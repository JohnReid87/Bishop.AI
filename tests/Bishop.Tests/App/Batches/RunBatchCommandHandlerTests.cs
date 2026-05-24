using Bishop.App.Batches.RunBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Services.Claude;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class RunBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private const string WorktreePath = @"C:\fake-worktrees\my-batch";

    public RunBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "x") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceAsync()
    {
        var name = U("ws");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    private async Task<Card> AddCardAsync(Guid workspaceId, string laneName)
    {
        var title = U("card");
        return await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspaceId, laneName, title), default);
    }

    private async Task<Batch> CreateBatchAsync(params Guid[] cardIds)
    {
        var repo = new BatchRepository(_factory);
        var slug = U("br");
        var batch = await repo.CreateAsync(U("batch"), $"bishop/{slug}", "main", WorktreePath);
        foreach (var id in cardIds)
            await repo.AssignCardAsync(batch.Id, id);
        return batch;
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_factory, sender)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<RecordClaudeRunCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new RecordClaudeRunCommandHandler(_factory)
                .Handle(call.ArgAt<RecordClaudeRunCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<RecordAutoRunFailureCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new RecordAutoRunFailureCommandHandler(_factory)
                .Handle(call.ArgAt<RecordAutoRunFailureCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<SetCardCommitCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new SetCardCommitCommandHandler(_factory)
                .Handle(call.ArgAt<SetCardCommitCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private static IGitCli GitAlwaysClean()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        return git;
    }

    private static IClaudeCliRunner ClaudeAlwaysSucceeds()
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(0, null, 0));
        return claude;
    }

    private static IClaudeCliRunner ClaudeReturnsExitCode(int exitCode)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(exitCode, null, 0));
        return claude;
    }

    private RunBatchCommandHandler CreateHandler(IGitCli? git = null, IClaudeCliRunner? claude = null, ISender? sender = null)
        => new(
            new BatchRepository(_factory),
            git ?? GitAlwaysClean(),
            claude ?? ClaudeAlwaysSucceeds(),
            sender ?? CreateSender(),
            _factory);

    // ── status validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task BatchNotFound_Throws()
    {
        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new RunBatchCommand("no-such-batch", Resume: false), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no-such-batch*");
    }

    [Fact]
    public async Task BatchAlreadyWorking_WithoutResume_ThrowsWithHint()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, SystemLaneNames.ToDo);
        var batch = await CreateBatchAsync(card.Id);
        await new BatchRepository(_factory).TransitionToWorkingAsync(batch.Id);

        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*--resume*");
    }

    [Fact]
    public async Task BatchClosed_WithoutResume_Throws()
    {
        var batch = await CreateBatchAsync();
        var repo = new BatchRepository(_factory);
        await repo.TransitionToWorkingAsync(batch.Id);
        await repo.CloseAsync(batch.Id, BatchClosedReason.Finished);

        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*closed*");
    }

    [Fact]
    public async Task ResumeWithOpenBatch_Throws()
    {
        var batch = await CreateBatchAsync();

        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new RunBatchCommand(batch.Name, Resume: true), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Working*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_ClosesWithFinished()
    {
        var batch = await CreateBatchAsync();

        var result = await CreateHandler().Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(0);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Finished);
    }

    [Fact]
    public async Task AllCardsSucceed_ClosesWithFinished()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        var result = await CreateHandler().Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(2);
        result.FailedCardNumbers.Should().BeNull();

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
    }

    [Fact]
    public async Task FreshRun_TransitionsBatchToWorking_ThenToClosedOnSuccess()
    {
        var batch = await CreateBatchAsync();

        await CreateHandler().Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
        saved.ClosedReason.Should().Be(BatchClosedReason.Finished);
    }

    // ── card failure ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CardFailure_StopsImmediately_BatchRemainsWorking()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        var result = await CreateHandler(claude: ClaudeReturnsExitCode(7))
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.CardFailure);
        result.Succeeded.Should().Be(0);
        result.FailedCardNumbers.Should().NotBeNullOrEmpty();

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
    }

    [Fact]
    public async Task CardFailure_ResetsAndCleansBatchWorktree()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());

        await CreateHandler(git: git, claude: ClaudeReturnsExitCode(1))
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await git.Received(1).ResetHardAsync(WorktreePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(WorktreePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CardFailure_RecordsFailureTimestamp()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var before = DateTimeOffset.UtcNow;
        await CreateHandler(claude: ClaudeReturnsExitCode(7))
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
        saved.LastAutoRunFailedAt!.Value.Should().BeOnOrAfter(before);
    }

    // ── git stop conditions ────────────────────────────────────────────────────

    [Fact]
    public async Task DirtyWorktree_StopsBeforeClaude()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Dirty(["src/foo.cs"]));
        var claude = ClaudeAlwaysSucceeds();

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.DirtyWorktree);
        result.DirtyPaths.Should().Equal("src/foo.cs");
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
    }

    [Fact]
    public async Task NotAGitRepo_StopsBeforeClaude()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.NotAGitRepo());
        var claude = ClaudeAlwaysSucceeds();

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.NotAGitRepo);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GitNotFound_StopsBeforeClaude()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.GitNotFound());
        var claude = ClaudeAlwaysSucceeds();

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.GitNotFound);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Claude invocation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ClaudeReceivesWorktreePath_NotWorkspacePath()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var claude = ClaudeAlwaysSucceeds();
        await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await claude.Received(1).RunPromptAsync(
            WorktreePath,
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeReceivesPromptWithCardNumber()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var claude = ClaudeAlwaysSucceeds();
        await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await claude.Received(1).RunPromptAsync(
            WorktreePath,
            $"/bish-auto-card #{card.Number}",
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModelOption_ThreadedToClaudeRunner()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var claude = ClaudeAlwaysSucceeds();
        await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false, "claude-sonnet-4-6"), default);

        await claude.Received(1).RunPromptAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "claude-sonnet-4-6",
            Arg.Any<CancellationToken>());
    }

    // ── token recording ────────────────────────────────────────────────────────

    [Fact]
    public async Task SuccessfulCard_AccumulatesClaudeTotals()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(0, new ClaudeRunTotals(1000, 250), 0));

        await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.TotalInputTokens.Should().Be(1000);
        saved.TotalOutputTokens.Should().Be(250);
        saved.ClaudeRunCount.Should().Be(1);
    }

    // ── commit recording ───────────────────────────────────────────────────────

    [Fact]
    public async Task SuccessfulCard_RecordsCommitHash_WhenCommitFound()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.GetCardCommitAsync(card.Number, WorktreePath, Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.Found(
                new CommitInfo("abc1234", "abc1234567890", $"feat: implement (card {card.Number})", "", DateTimeOffset.UtcNow, false)));

        await CreateHandler(git: git).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.CommitHash.Should().Be("abc1234567890");
        saved.BranchName.Should().Be(batch.BranchName);
    }

    [Fact]
    public async Task SuccessfulCard_DoesNotThrow_WhenCommitNotFound()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());

        var result = await CreateHandler(git: git).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
    }

    // ── resume ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_SkipsDoneCards_ProcessesRemaining()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        var repo = new BatchRepository(_factory);
        await repo.TransitionToWorkingAsync(batch.Id);

        // Manually move c1 to Done to simulate a previously completed card
        await new MoveCardCommandHandler(_factory, CreateSender())
            .Handle(new MoveCardCommand(c1.Id, SystemLaneNames.Done, 0, KeepOpen: true), default);

        var claude = ClaudeAlwaysSucceeds();
        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: true), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(1);

        await claude.Received(1).RunPromptAsync(
            WorktreePath,
            $"/bish-auto-card #{c2.Number}",
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resume_AllCardsDone_ClosesImmediately()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var card = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(card.Id);

        var repo = new BatchRepository(_factory);
        await repo.TransitionToWorkingAsync(batch.Id);
        await new MoveCardCommandHandler(_factory, CreateSender())
            .Handle(new MoveCardCommand(card.Id, SystemLaneNames.Done, 0, KeepOpen: true), default);

        var claude = ClaudeAlwaysSucceeds();
        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: true), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(0);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Closed);
    }
}
