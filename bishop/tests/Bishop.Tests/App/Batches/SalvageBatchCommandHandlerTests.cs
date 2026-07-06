using Bishop.App.Batches.CleanUpBatch;
using Bishop.App.Batches.MergeBatch;
using Bishop.App.Batches.SalvageBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class SalvageBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private readonly string _worktreePath;
    private const string WorkspacePath = @"C:\fake-workspace";

    public SalvageBatchCommandHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
        _worktreePath = Path.Combine(Path.GetTempPath(), "bishop-salvage-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_worktreePath, ".bishop"));
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateWorkingBatchAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _wsId,
            Name = U("batch"),
            BranchName = $"bishop/{U("br")}",
            BaseBranch = "main",
            WorktreePath = _worktreePath,
            Status = BatchStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        batch.TransitionToWorking();
        await db.SaveChangesAsync();
        return batch;
    }

    private async Task<Card> AddCardToBatchAsync(Batch batch, string laneName)
    {
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(_wsId, laneName, U("card")), default);

        await using var db = await _factory.CreateDbContextAsync();
        var dbCard = await db.Cards.FindAsync(card.Id);
        dbCard!.BatchId = batch.Id;
        dbCard.LaneName = laneName;
        await db.SaveChangesAsync();
        return card;
    }

    private string LockPath(Guid batchId) => Path.Combine(_worktreePath, ".bishop", $"batch-{batchId}.lock");

    private void WriteLock(Guid batchId, int pid) =>
        File.WriteAllText(LockPath(batchId), $"{pid}\t{DateTimeOffset.UtcNow:O}");

    /// <summary>Git substitute wired for the salvage happy path: merge succeeds, branch is merged, worktree clean.</summary>
    private static IGitCli GitHappy()
    {
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(true, []));
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        git.LocalBranchExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        git.GetWorktreeBranchesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);
        git.RemoveWorktreeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return git;
    }

    private SalvageBatchCommandHandler CreateHandler(IGitCli git) =>
        new(_factory, new SalvageTestSender(_factory, git), git);

    // ── validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler(GitHappy())
            .Handle(new SalvageBatchCommand("no-such", WorkspacePath, Confirm: true), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    [Fact]
    public async Task OpenBatch_Throws()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var batch = new Batch
        {
            Id = Guid.NewGuid(), WorkspaceId = _wsId, Name = U("batch"), BranchName = U("br"),
            BaseBranch = "main", WorktreePath = _worktreePath, Status = BatchStatus.Open, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateHandler(GitHappy())
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: true), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    // ── live lock guard ────────────────────────────────────────────────────────

    [Fact]
    public async Task LockPidAlive_RefusesWithoutMerging()
    {
        var batch = await CreateWorkingBatchAsync();
        await AddCardToBatchAsync(batch, SystemLaneNames.Done);
        WriteLock(batch.Id, Environment.ProcessId); // this test process is alive

        var git = GitHappy();
        var result = await CreateHandler(git)
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: true), default);

        result.Outcome.Should().Be(SalvageBatchOutcome.LockAlive);
        result.LockOwnerPid.Should().Be(Environment.ProcessId);

        await git.DidNotReceive().MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await using var db = await _factory.CreateDbContextAsync();
        var saved = await db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
    }

    // ── nothing succeeded ──────────────────────────────────────────────────────

    [Fact]
    public async Task NoFinishedCards_ReturnsNothingSucceeded_NoMerge()
    {
        var batch = await CreateWorkingBatchAsync();
        var failCard = await AddCardToBatchAsync(batch, SystemLaneNames.Doing);

        var git = GitHappy();
        var result = await CreateHandler(git)
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: true), default);

        result.Outcome.Should().Be(SalvageBatchOutcome.NothingSucceeded);
        result.EjectedCardNumbers.Should().ContainSingle().Which.Should().Be(failCard.Number);

        await git.DidNotReceive().MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await using var db = await _factory.CreateDbContextAsync();
        var saved = await db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        var savedCard = await db.Cards.SingleAsync(c => c.Id == failCard.Id);
        savedCard.LaneName.Should().Be(SystemLaneNames.Doing);
        savedCard.BatchId.Should().Be(batch.Id);
    }

    // ── preview (no confirm) ───────────────────────────────────────────────────

    [Fact]
    public async Task WithoutConfirm_ReturnsSplit_MakesNoChanges()
    {
        var batch = await CreateWorkingBatchAsync();
        var doneCard = await AddCardToBatchAsync(batch, SystemLaneNames.Done);
        var failCard = await AddCardToBatchAsync(batch, SystemLaneNames.Doing);

        var git = GitHappy();
        var result = await CreateHandler(git)
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: false), default);

        result.Outcome.Should().Be(SalvageBatchOutcome.NeedsConfirmation);
        result.MergedCardNumbers.Should().ContainSingle().Which.Should().Be(doneCard.Number);
        result.EjectedCardNumbers.Should().ContainSingle().Which.Should().Be(failCard.Number);

        await git.DidNotReceive().MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await using var db = await _factory.CreateDbContextAsync();
        var saved = await db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        var savedFail = await db.Cards.SingleAsync(c => c.Id == failCard.Id);
        savedFail.LaneName.Should().Be(SystemLaneNames.Doing);
        savedFail.BatchId.Should().Be(batch.Id);
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmed_MergesFinished_EjectsRest_ClosesBatch()
    {
        var batch = await CreateWorkingBatchAsync();
        var doneCard = await AddCardToBatchAsync(batch, SystemLaneNames.Done);
        var failCard = await AddCardToBatchAsync(batch, SystemLaneNames.Doing);
        var pendingCard = await AddCardToBatchAsync(batch, SystemLaneNames.Backlog);

        var git = GitHappy();
        var result = await CreateHandler(git)
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: true), default);

        result.Outcome.Should().Be(SalvageBatchOutcome.Salvaged);
        result.MergedCardNumbers.Should().ContainSingle().Which.Should().Be(doneCard.Number);
        result.EjectedCardNumbers.Should().BeEquivalentTo([failCard.Number, pendingCard.Number]);
        result.ClosedCardNumbers.Should().ContainSingle().Which.Should().Be(doneCard.Number);

        await git.Received(1).MergeAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>());

        await using var db = await _factory.CreateDbContextAsync();
        var savedBatch = await db.Batches.SingleAsync(b => b.Id == batch.Id);
        savedBatch.Status.Should().Be(BatchStatus.Closed);
        savedBatch.ClosedReason.Should().Be(BatchClosedReason.Finished);

        var savedDone = await db.Cards.SingleAsync(c => c.Id == doneCard.Id);
        savedDone.LaneName.Should().Be(SystemLaneNames.Done);
        savedDone.IsClosed.Should().BeTrue();

        foreach (var ejected in new[] { failCard, pendingCard })
        {
            var savedCard = await db.Cards.SingleAsync(c => c.Id == ejected.Id);
            savedCard.LaneName.Should().Be(SystemLaneNames.ToDo);
            savedCard.BatchId.Should().BeNull();
        }
    }

    [Fact]
    public async Task Confirmed_DirtyWorktree_ResetsBeforeCleanUp()
    {
        var batch = await CreateWorkingBatchAsync();
        await AddCardToBatchAsync(batch, SystemLaneNames.Done);
        await AddCardToBatchAsync(batch, SystemLaneNames.Doing);

        var git = GitHappy();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Dirty(["src/failed.cs"]));

        var result = await CreateHandler(git)
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: true), default);

        result.Outcome.Should().Be(SalvageBatchOutcome.Salvaged);
        await git.Received(1).ResetHardAsync(_worktreePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(_worktreePath, Arg.Any<CancellationToken>());
    }

    // ── merge conflict aborts cleanly ──────────────────────────────────────────

    [Fact]
    public async Task Confirmed_MergeConflict_AbortsWithNoStateChange()
    {
        var batch = await CreateWorkingBatchAsync();
        var doneCard = await AddCardToBatchAsync(batch, SystemLaneNames.Done);
        var failCard = await AddCardToBatchAsync(batch, SystemLaneNames.Doing);

        var git = GitHappy();
        git.MergeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult(false, ["src/Foo.cs"]));

        var result = await CreateHandler(git)
            .Handle(new SalvageBatchCommand(batch.Name, WorkspacePath, Confirm: true), default);

        result.Outcome.Should().Be(SalvageBatchOutcome.MergeConflict);
        result.ConflictFiles.Should().Equal("src/Foo.cs");

        await git.DidNotReceive().RemoveWorktreeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await git.DidNotReceive().ResetHardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        await using var db = await _factory.CreateDbContextAsync();
        var savedBatch = await db.Batches.SingleAsync(b => b.Id == batch.Id);
        savedBatch.Status.Should().Be(BatchStatus.Working);

        var savedDone = await db.Cards.SingleAsync(c => c.Id == doneCard.Id);
        savedDone.LaneName.Should().Be(SystemLaneNames.Done);
        savedDone.IsClosed.Should().BeFalse();

        var savedFail = await db.Cards.SingleAsync(c => c.Id == failCard.Id);
        savedFail.LaneName.Should().Be(SystemLaneNames.Doing);
        savedFail.BatchId.Should().Be(batch.Id);
    }

    /// <summary>Minimal <see cref="ISender"/> wiring the real merge / clean-up / close-card handlers salvage delegates to.</summary>
    private sealed class SalvageTestSender : ISender
    {
        private readonly MergeBatchCommandHandler _merge;
        private readonly CleanUpBatchCommandHandler _cleanUp;
        private readonly CloseCardCommandHandler _closeCard;

        public SalvageTestSender(IDbContextFactory<BishopDbContext> factory, IGitCli git)
        {
            _merge = new(factory, git, TimeProvider.System);
            _closeCard = new(factory);
            _cleanUp = new(factory, this, git, NullLogger<CleanUpBatchCommandHandler>.Instance, TimeProvider.System);
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is MergeBatchCommand merge)
                return (TResponse)(object)await _merge.Handle(merge, ct);
            if (request is CleanUpBatchCommand cleanUp)
                return (TResponse)(object)await _cleanUp.Handle(cleanUp, ct);
            if (request is CloseCardCommand close)
                return (TResponse)(object)await _closeCard.Handle(close, ct);
            throw new NotSupportedException($"SalvageTestSender does not handle {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            Task.FromResult<object?>(null);

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest =>
            Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) =>
            AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) =>
            AsyncEnumerable.Empty<object?>();
    }
}
