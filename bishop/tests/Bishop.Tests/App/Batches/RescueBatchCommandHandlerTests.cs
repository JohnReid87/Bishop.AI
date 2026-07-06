using Bishop.App.Batches.RescueBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class RescueBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private readonly string _worktreePath;

    public RescueBatchCommandHandlerTests(DbFixture fixture)
    {
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
        _worktreePath = Path.Combine(Path.GetTempPath(), "bishop-rescue-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_worktreePath, ".bishop"));
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    /// <summary>Creates a Working batch with a single card in <paramref name="cardLane"/>, optionally flagged as auto-run-failed.</summary>
    private async Task<(Batch batch, Card card)> SetupWorkingBatchAsync(
        string cardLane = SystemLaneNames.Doing, bool markFailed = false)
    {
        var wsName = U("ws");
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(wsName, $@"C:\{wsName}"), default);

        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, U("card")), default);

        await using var db = await _factory.CreateDbContextAsync(default);
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

        var dbCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        dbCard.BatchId = batch.Id;
        dbCard.LaneName = cardLane;
        if (markFailed)
            dbCard.LastAutoRunFailedAt = DateTimeOffset.UtcNow;
        batch.TransitionToWorking();
        await db.SaveChangesAsync();

        return (batch, card);
    }

    private string LockPath(Guid batchId) => Path.Combine(_worktreePath, ".bishop", $"batch-{batchId}.lock");

    private void WriteLock(Guid batchId, int pid) =>
        File.WriteAllText(LockPath(batchId), $"{pid}\t{DateTimeOffset.UtcNow:O}");

    private static IGitCli GitClean()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        return git;
    }

    private static IGitCli GitDirty(params string[] paths)
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Dirty(paths));
        return git;
    }

    private RescueBatchCommandHandler CreateHandler(IGitCli git) =>
        new(git, new RescueTestSender(_factory), _factory);

    // ── validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler(GitClean())
            .Handle(new RescueBatchCommand("no-such-batch", ConfirmReset: true), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task NonWorkingBatch_ReturnsNotRunning()
    {
        var wsName = U("ws");
        await using var db = await _factory.CreateDbContextAsync(default);
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

        var result = await CreateHandler(GitClean())
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: true), default);

        result.Outcome.Should().Be(RescueBatchOutcome.NotRunning);
    }

    // ── live lock guard ────────────────────────────────────────────────────────

    [Fact]
    public async Task LockPidAlive_RefusesAndMakesNoChanges()
    {
        var (batch, card) = await SetupWorkingBatchAsync(SystemLaneNames.Doing);
        WriteLock(batch.Id, Environment.ProcessId); // this test process is alive

        var git = GitDirty("src/foo.cs");
        var result = await CreateHandler(git)
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: true), default);

        result.Outcome.Should().Be(RescueBatchOutcome.LockAlive);
        result.LockOwnerPid.Should().Be(Environment.ProcessId);

        await git.DidNotReceive().ResetHardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        File.Exists(LockPath(batch.Id)).Should().BeTrue();

        await using var db = await _factory.CreateDbContextAsync(default);
        var savedCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        savedCard.LaneName.Should().Be(SystemLaneNames.Doing);
    }

    // ── dirty worktree confirmation ────────────────────────────────────────────

    [Fact]
    public async Task DirtyWorktree_WithoutConfirm_ReturnsNeedsConfirmation_NoChanges()
    {
        var (batch, card) = await SetupWorkingBatchAsync(SystemLaneNames.Doing);
        WriteLock(batch.Id, int.MaxValue); // dead PID

        var git = GitDirty("src/a.cs", "src/b.cs");
        var result = await CreateHandler(git)
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: false), default);

        result.Outcome.Should().Be(RescueBatchOutcome.NeedsConfirmation);
        result.DirtyPaths.Should().Equal("src/a.cs", "src/b.cs");

        await git.DidNotReceive().ResetHardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        File.Exists(LockPath(batch.Id)).Should().BeTrue();

        await using var db = await _factory.CreateDbContextAsync(default);
        var savedCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        savedCard.LaneName.Should().Be(SystemLaneNames.Doing);
    }

    [Fact]
    public async Task DirtyWorktree_WithConfirm_ResetsCleansRequeuesAndClearsLock()
    {
        var (batch, card) = await SetupWorkingBatchAsync(SystemLaneNames.Doing, markFailed: true);
        WriteLock(batch.Id, int.MaxValue); // dead PID

        var git = GitDirty("src/foo.cs");
        var result = await CreateHandler(git)
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: true), default);

        result.Outcome.Should().Be(RescueBatchOutcome.Rescued);
        result.WorktreeReset.Should().BeTrue();
        result.LockCleared.Should().BeTrue();
        result.RequeuedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);

        await git.Received(1).ResetHardAsync(_worktreePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(_worktreePath, Arg.Any<CancellationToken>());
        File.Exists(LockPath(batch.Id)).Should().BeFalse();

        await using var db = await _factory.CreateDbContextAsync(default);
        var savedCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        savedCard.LaneName.Should().Be(SystemLaneNames.ToDo);
        savedCard.BatchId.Should().Be(batch.Id);
        savedCard.LastAutoRunFailedAt.Should().BeNull();
    }

    // ── clean worktree, dead lock ──────────────────────────────────────────────

    [Fact]
    public async Task CleanWorktree_DeadLock_RequeuesAndClearsLock_WithoutConfirm()
    {
        var (batch, card) = await SetupWorkingBatchAsync(SystemLaneNames.Doing, markFailed: true);
        WriteLock(batch.Id, int.MaxValue); // dead PID

        var git = GitClean();
        var result = await CreateHandler(git)
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: false), default);

        result.Outcome.Should().Be(RescueBatchOutcome.Rescued);
        result.WorktreeReset.Should().BeFalse();
        result.LockCleared.Should().BeTrue();
        result.RequeuedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);

        await git.DidNotReceive().ResetHardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        File.Exists(LockPath(batch.Id)).Should().BeFalse();

        await using var db = await _factory.CreateDbContextAsync(default);
        var savedCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        savedCard.LaneName.Should().Be(SystemLaneNames.ToDo);
        savedCard.LastAutoRunFailedAt.Should().BeNull();
    }

    // ── no lock file at all ────────────────────────────────────────────────────

    [Fact]
    public async Task NoLockFile_StillRescues_LockClearedFalse()
    {
        var (batch, card) = await SetupWorkingBatchAsync(SystemLaneNames.Doing);
        // no lock file written

        var result = await CreateHandler(GitClean())
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: false), default);

        result.Outcome.Should().Be(RescueBatchOutcome.Rescued);
        result.LockCleared.Should().BeFalse();
        result.RequeuedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);

        await using var db = await _factory.CreateDbContextAsync(default);
        var savedCard = await db.Cards.FirstAsync(c => c.Id == card.Id);
        savedCard.LaneName.Should().Be(SystemLaneNames.ToDo);
    }

    // ── nothing stranded ───────────────────────────────────────────────────────

    [Fact]
    public async Task NoStuckCard_CleanWorktree_ReportsNoRequeue()
    {
        var (batch, _) = await SetupWorkingBatchAsync(SystemLaneNames.ToDo);
        WriteLock(batch.Id, int.MaxValue);

        var result = await CreateHandler(GitClean())
            .Handle(new RescueBatchCommand(batch.Name, ConfirmReset: false), default);

        result.Outcome.Should().Be(RescueBatchOutcome.Rescued);
        result.RequeuedCardNumbers.Should().BeEmpty();
        result.LockCleared.Should().BeTrue();
    }

    /// <summary>Minimal <see cref="ISender"/> wiring the real card-move handlers rescue delegates to.</summary>
    private sealed class RescueTestSender : ISender
    {
        private readonly MoveCardCommandHandler _moveCard;
        private readonly UpdateCardCommandHandler _updateCard;

        public RescueTestSender(IDbContextFactory<BishopDbContext> factory)
        {
            _moveCard = new(factory);
            _updateCard = new(factory, this);
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is MoveCardCommand move)
                return (TResponse)(object)await _moveCard.Handle(move, ct);
            if (request is UpdateCardCommand update)
                return (TResponse)(object)await _updateCard.Handle(update, ct);
            throw new NotSupportedException($"RescueTestSender does not handle {request.GetType().Name}");
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
