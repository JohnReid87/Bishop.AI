using Bishop.App.Batches.FinishBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Git;
using Bishop.App.Git.Push;
using Bishop.App.Services.GitHub;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class FinishBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";
    private const string GitHubRepo = "owner/repo";

    public FinishBatchCommandHandlerTests(DbFixture fixture)
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

    private async Task<Card> AddCardAsync(Guid workspaceId, string? commitHash = null)
    {
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, SystemLaneNames.ToDo, U("card")), default);
        if (commitHash is not null)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var saved = await db.Cards.FindAsync(card.Id);
            saved!.CommitHash = commitHash;
            await db.SaveChangesAsync();
        }
        return card;
    }

    private async Task<Batch> CreateWorkingBatchAsync(params Guid[] cardIds)
    {
        var repo = new BatchRepository(_factory);
        var slug = U("br");
        var batch = await repo.CreateAsync(U("batch"), $"bishop/{slug}", "main", WorktreePath);
        foreach (var id in cardIds)
            await repo.AssignCardAsync(batch.Id, id);
        await repo.TransitionToWorkingAsync(batch.Id);
        return await repo.GetAsync(batch.Id) ?? throw new InvalidOperationException("Batch not found");
    }

    private static IGitCli GitPushSucceeds()
    {
        var git = Substitute.For<IGitCli>();
        git.PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult(true, null));
        return git;
    }

    private static IGhCli GhReturnsUrl(string url = "https://github.com/owner/repo/pull/1")
    {
        var gh = Substitute.For<IGhCli>();
        gh.RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(url);
        return gh;
    }

    private FinishBatchCommandHandler CreateHandler(IGitCli? git = null, IGhCli? gh = null)
        => new(new BatchRepository(_factory), git ?? GitPushSucceeds(), gh ?? GhReturnsUrl(), _factory);

    // ── status validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new FinishBatchCommand("no-such", WorkspacePath, GitHubRepo), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such*");
    }

    [Fact]
    public async Task BatchOpen_Throws()
    {
        var batch = await new BatchRepository(_factory).CreateAsync(U("batch"), U("br"), "main", WorktreePath);

        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task BatchAlreadyClosed_Throws()
    {
        var repo = new BatchRepository(_factory);
        var batch = await repo.CreateAsync(U("batch"), U("br"), "main", WorktreePath);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    [Fact]
    public async Task PushFails_Throws_BeforeCreatingPr()
    {
        var batch = await CreateWorkingBatchAsync();

        var git = Substitute.For<IGitCli>();
        git.PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult(false, "rejected: not fast-forward"));
        var gh = GhReturnsUrl();

        var handler = CreateHandler(git: git, gh: gh);

        Func<Task> act = () => handler.Handle(new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rejected*");
        await gh.DidNotReceive().RunCaptureAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_CreatesPr_BatchRemainsWorking()
    {
        var batch = await CreateWorkingBatchAsync();
        var gh = GhReturnsUrl("https://github.com/owner/repo/pull/99");

        var result = await CreateHandler(gh: gh).Handle(
            new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        result.PrUrl.Should().Be("https://github.com/owner/repo/pull/99");

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        saved.ClosedReason.Should().BeNull();
        saved.ClosedAt.Should().BeNull();
        saved.GitHubPrUrl.Should().Be("https://github.com/owner/repo/pull/99");
    }

    [Fact]
    public async Task PrBodyContainsCardChecklist()
    {
        var workspace = await CreateWorkspaceAsync();
        var c1 = await AddCardAsync(workspace.Id, commitHash: "abc1234567890");
        var c2 = await AddCardAsync(workspace.Id);
        var batch = await CreateWorkingBatchAsync(c1.Id, c2.Id);

        string? capturedBody = null;
        var gh = Substitute.For<IGhCli>();
        gh.RunCaptureAsync(Arg.Do<string[]>(args =>
        {
            var bodyIndex = Array.IndexOf(args, "--body");
            if (bodyIndex >= 0 && bodyIndex + 1 < args.Length)
                capturedBody = args[bodyIndex + 1];
        }), Arg.Any<CancellationToken>())
            .Returns("https://github.com/owner/repo/pull/1");

        await CreateHandler(gh: gh).Handle(
            new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        capturedBody.Should().Contain($"- [x] #{c1.Number} {c1.Title} — abc1234");
        capturedBody.Should().Contain($"- [x] #{c2.Number} {c2.Title}");
        capturedBody.Should().NotContain("— null");
    }

    [Fact]
    public async Task PrReceivesCorrectHeadAndBase()
    {
        var batch = await CreateWorkingBatchAsync();

        var gh = GhReturnsUrl();
        await CreateHandler(gh: gh).Handle(
            new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        await gh.Received(1).RunCaptureAsync(
            Arg.Is<string[]>(args =>
                args.Contains("--head") && args.SkipWhile(a => a != "--head").Skip(1).FirstOrDefault() == batch.BranchName
                && args.Contains("--base") && args.SkipWhile(a => a != "--base").Skip(1).FirstOrDefault() == batch.BaseBranch),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushCalledWithWorktreePath()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = GitPushSucceeds();

        await CreateHandler(git: git).Handle(
            new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        await git.Received(1).PushAsync(WorktreePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorktreeRemovedWithWorkspacePath()
    {
        var batch = await CreateWorkingBatchAsync();
        var git = GitPushSucceeds();

        await CreateHandler(git: git).Handle(
            new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        await git.Received(1).RemoveWorktreeAsync(WorkspacePath, WorktreePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrUrlTrimmed()
    {
        var batch = await CreateWorkingBatchAsync();

        var result = await CreateHandler(gh: GhReturnsUrl("  https://github.com/owner/repo/pull/5\n"))
            .Handle(new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        result.PrUrl.Should().Be("https://github.com/owner/repo/pull/5");
    }

    [Fact]
    public async Task GitHubPrUrl_PersistedOnBatch_AfterFinish()
    {
        const string prUrl = "https://github.com/owner/repo/pull/7";
        var batch = await CreateWorkingBatchAsync();

        await CreateHandler(gh: GhReturnsUrl(prUrl))
            .Handle(new FinishBatchCommand(batch.Name, WorkspacePath, GitHubRepo), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.GitHubPrUrl.Should().Be(prUrl);
    }
}
