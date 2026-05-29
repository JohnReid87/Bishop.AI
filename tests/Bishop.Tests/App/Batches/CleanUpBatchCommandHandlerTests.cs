using Bishop.App.Batches.CleanUpBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Git;
using Bishop.App.Services.GitHub;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.Batches;

public sealed class CleanUpBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly Guid _wsId;
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public CleanUpBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _wsId = fixture.SeedWorkspace();
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Batch> CreateWorkingBatchAsync()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(_wsId, U("batch"), $"bishop/{U("br")}", "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        return await repo.GetAsync(batch.Id) ?? throw new InvalidOperationException("Batch not found");
    }

    private async Task<Card> AddCardAsync(string laneName)
        => await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(_wsId, laneName, U("card")), default);

    private async Task AssignCardToBatchAsync(Batch batch, Card card)
        => await new BatchRepository(_factory).AssignCardAsync(batch.Id, card.Id);

    private static IGitCli GitMergedNoBranchNoWorktree()
    {
        var git = Substitute.For<IGitCli>();
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

    private CleanUpBatchCommandHandler CreateHandler(IGitCli? git = null, IGhCli? ghCli = null)
    {
        var gh = ghCli ?? Substitute.For<IGhCli>();
        ISender sender = new CleanUpBatchTestSender(_factory, gh);
        return new(new BatchRepository(_factory), _factory, sender,
                   git ?? GitMergedNoBranchNoWorktree(),
                   NullLogger<CleanUpBatchCommandHandler>.Instance);
    }

    private sealed class CleanUpBatchTestSender : ISender
    {
        private readonly CloseCardCommandHandler _closeCard;

        public CleanUpBatchTestSender(IDbContextFactory<BishopDbContext> factory, IGhCli ghCli)
            => _closeCard = new(factory, ghCli);

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CloseCardCommand cmd)
                return (TResponse)(object)(await _closeCard.Handle(cmd, ct));
            throw new NotSupportedException($"CleanUpBatchTestSender does not handle {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            Task.FromResult<object?>(null);

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest =>
            throw new NotSupportedException($"CleanUpBatchTestSender does not handle {request!.GetType().Name}");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) =>
            AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) =>
            AsyncEnumerable.Empty<object?>();
    }

    // ── guard: batch not found ─────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new CleanUpBatchCommand("no-such", WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    // ── guard: not merged ──────────────────────────────────────────────────────

    [Fact]
    public async Task NotMerged_Throws()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = Substitute.For<IGitCli>();
        git.IsBranchMergedIntoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Func<Task> act = () => CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not been merged*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ClosesBatch_WithFinishedReason()
    {
        var batch = await CreateWorkingBatchAsync();

        await CreateHandler().Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Finished);
        saved.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BranchExists_DeletesBranch()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = GitMergedNoBranchNoWorktree();
        git.LocalBranchExistsAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>())
            .Returns(true);
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);

        await CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).DeleteLocalBranchAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BranchCheckedOut_SkipsBranchDeletion()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = GitMergedNoBranchNoWorktree();
        git.LocalBranchExistsAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>())
            .Returns(true);
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[batch.BranchName]);

        await CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await git.DidNotReceive().DeleteLocalBranchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorktreeRemoveFails_BatchStillClosed()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = GitMergedNoBranchNoWorktree();
        git.RemoveWorktreeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("worktree not found"));

        await CreateHandler(git).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
    }

    // ── card closure ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AllDoneCards_AreClosedAndReturnedInResult()
    {
        var batch = await CreateWorkingBatchAsync();
        var card1 = await AddCardAsync(SystemLaneNames.Done);
        var card2 = await AddCardAsync(SystemLaneNames.Done);
        await AssignCardToBatchAsync(batch, card1);
        await AssignCardToBatchAsync(batch, card2);

        var result = await CreateHandler().Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        result.ClosedCardNumbers.Should().BeEquivalentTo([card1.Number, card2.Number]);

        var saved1 = await _db.Cards.SingleAsync(c => c.Id == card1.Id);
        var saved2 = await _db.Cards.SingleAsync(c => c.Id == card2.Id);
        saved1.IsClosed.Should().BeTrue();
        saved2.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task MixedLane_OnlyDoneCardsAreClosed()
    {
        var batch = await CreateWorkingBatchAsync();
        var doneCard = await AddCardAsync(SystemLaneNames.Done);
        var doingCard = await AddCardAsync(SystemLaneNames.Doing);
        await AssignCardToBatchAsync(batch, doneCard);
        await AssignCardToBatchAsync(batch, doingCard);

        var result = await CreateHandler().Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        result.ClosedCardNumbers.Should().ContainSingle().Which.Should().Be(doneCard.Number);

        var savedDone = await _db.Cards.SingleAsync(c => c.Id == doneCard.Id);
        var savedDoing = await _db.Cards.SingleAsync(c => c.Id == doingCard.Id);
        savedDone.IsClosed.Should().BeTrue();
        savedDoing.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task DoneCard_WithGhIssue_CallsGhCli()
    {
        var batch = await CreateWorkingBatchAsync();
        var card = await AddCardAsync(SystemLaneNames.Done);
        await AssignCardToBatchAsync(batch, card);

        await _db.Cards
            .Where(c => c.Id == card.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.GitHubIssueNumber, 99));
        await _db.Workspaces
            .Where(w => w.Id == _wsId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.GitHubRepo, "owner/repo"));

        var ghCli = Substitute.For<IGhCli>();
        await CreateHandler(ghCli: ghCli).Handle(new CleanUpBatchCommand(batch.Name, WorkspacePath), default);

        await ghCli.Received(1).RunAsync(
            Arg.Is<string[]>(args => args.Contains("close") && args.Contains("99")),
            Arg.Any<CancellationToken>());
    }
}
