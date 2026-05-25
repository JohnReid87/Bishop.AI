using Bishop.App.Batches.CompleteBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Git;
using Bishop.App.Services.GitHub;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.Batches;

public sealed class CompleteBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public CompleteBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var name = U("ws");
        return await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
    }

    private async Task<Card> AddCardAsync(Guid workspaceId, string laneName = SystemLaneNames.ToDo)
    {
        return await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, laneName, U("card")), default);
    }

    private async Task<Batch> CreateWorkingBatchWithPrAsync(string prUrl, params Guid[] cardIds)
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(U("batch"), $"bishop/{U("br")}", "main", WorktreePath);
        foreach (var id in cardIds)
            await repo.AssignCardAsync(batch.Id, id);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.SetGitHubPrUrlAsync(batch.Id, prUrl);
        return await repo.GetAsync(batch.Id) ?? throw new InvalidOperationException("Batch not found");
    }

    private ISender CreateSenderWithCloseCard(IGhCli ghCli)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new CloseCardCommandHandler(_factory, ghCli)
                .Handle(call.ArgAt<CloseCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private static IGhCli GhMergeSucceeds()
    {
        var gh = Substitute.For<IGhCli>();
        gh.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        return gh;
    }

    private static IGitCli GitDeleteSucceeds()
    {
        var git = Substitute.For<IGitCli>();
        git.DeleteLocalBranchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return git;
    }

    private CompleteBatchCommandHandler CreateHandler(IGhCli? gh = null, IGitCli? git = null, ISender? sender = null)
    {
        var ghReal = gh ?? GhMergeSucceeds();
        return new(
            new BatchRepository(_factory),
            ghReal,
            git ?? GitDeleteSucceeds(),
            sender ?? CreateSenderWithCloseCard(ghReal),
            _factory,
            NullLogger<CompleteBatchCommandHandler>.Instance);
    }

    // ── status validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        Func<Task> act = () => CreateHandler().Handle(new CompleteBatchCommand("no-such", WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    [Fact]
    public async Task BatchOpen_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(U("batch"), U("br"), "main", WorktreePath);

        Func<Task> act = () => CreateHandler().Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchClosed_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        Func<Task> act = () => CreateHandler().Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchHasNoPrUrl_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);

        Func<Task> act = () => CreateHandler().Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PR URL*");
    }

    // ── merge failure ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PrMergeFails_NoStateChange()
    {
        const string prUrl = "https://github.com/owner/repo/pull/5";
        var batch = await CreateWorkingBatchWithPrAsync(prUrl);

        var gh = Substitute.For<IGhCli>();
        gh.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gh exited 1: merge conflict"));

        Func<Task> act = () => CreateHandler(gh: gh).Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*merge conflict*");

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        saved.ClosedAt.Should().BeNull();
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ClosesBatch_WithFinishedReason()
    {
        const string prUrl = "https://github.com/owner/repo/pull/7";
        var batch = await CreateWorkingBatchWithPrAsync(prUrl);

        await CreateHandler().Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Finished);
        saved.ClosedAt.Should().NotBeNull();
        saved.GitHubPrUrl.Should().Be(prUrl);
    }

    [Fact]
    public async Task Success_ClosesCardsInDone_LeavesOthersOpen()
    {
        const string prUrl = "https://github.com/owner/repo/pull/8";
        var workspace = await CreateWorkspaceAsync();
        var doneCard = await AddCardAsync(workspace.Id, SystemLaneNames.Done);
        var doingCard = await AddCardAsync(workspace.Id, SystemLaneNames.Doing);
        var batch = await CreateWorkingBatchWithPrAsync(prUrl, doneCard.Id, doingCard.Id);

        await CreateHandler().Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        var savedDone = await _db.Cards.SingleAsync(c => c.Id == doneCard.Id);
        var savedDoing = await _db.Cards.SingleAsync(c => c.Id == doingCard.Id);
        savedDone.IsClosed.Should().BeTrue();
        savedDoing.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task Success_MergeCalledWithPrUrl_SquashDeleteBranch()
    {
        const string prUrl = "https://github.com/owner/repo/pull/9";
        var batch = await CreateWorkingBatchWithPrAsync(prUrl);
        var gh = GhMergeSucceeds();

        await CreateHandler(gh: gh).Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        await gh.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(args =>
                args.Contains("pr") && args.Contains("merge") && args.Contains(prUrl)
                && args.Contains("--squash") && args.Contains("--delete-branch")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Success_DeleteLocalBranchCalledWithWorkspacePath()
    {
        const string prUrl = "https://github.com/owner/repo/pull/10";
        var batch = await CreateWorkingBatchWithPrAsync(prUrl);
        var git = GitDeleteSucceeds();

        await CreateHandler(git: git).Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        await git.Received(1).DeleteLocalBranchAsync(WorkspacePath, batch.BranchName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BranchDeleteFails_BatchStillCompleted()
    {
        const string prUrl = "https://github.com/owner/repo/pull/11";
        var batch = await CreateWorkingBatchWithPrAsync(prUrl);

        var git = Substitute.For<IGitCli>();
        git.DeleteLocalBranchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("git: no such branch"));

        await CreateHandler(git: git).Handle(new CompleteBatchCommand(batch.Name, WorkspacePath), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
    }
}
