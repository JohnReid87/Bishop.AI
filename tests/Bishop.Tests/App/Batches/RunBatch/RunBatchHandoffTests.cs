using Bishop.App.Batches.RunBatch;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Services.Claude;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Batches.RunBatch;

public sealed class RunBatchHandoffTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _worktreePath;

    public RunBatchHandoffTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _worktreePath = Path.Combine(Path.GetTempPath(), "bishop-handoff-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_worktreePath, ".bishop"));
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
        var batch = await repo.CreateAsync(U("batch"), $"bishop/{slug}", "main", _worktreePath);
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
        sender.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new UpdateCardCommandHandler(_factory, sender)
                .Handle(call.ArgAt<UpdateCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private static IGitCli GitAlwaysClean(string commitHash = "deadbeef12345678deadbeef12345678deadbeef")
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(commitHash);
        return git;
    }

    private IClaudeCliRunner ClaudeSucceedsWithHandoff(string handoffJson)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var path = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                await File.WriteAllTextAsync(path, handoffJson);
                return new ClaudeRunResult(0, null, 0);
            });
        return claude;
    }

    private static IClaudeCliRunner ClaudeSucceedsNoHandoff()
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(0, null, 0));
        return claude;
    }

    private RunBatchCommandHandler CreateHandler(IGitCli? git = null, IClaudeCliRunner? claude = null, ISender? sender = null)
        => new(
            new BatchRepository(_factory),
            git ?? GitAlwaysClean(),
            claude ?? ClaudeSucceedsNoHandoff(),
            sender ?? CreateSender(),
            _factory);

    // ── valid handoff ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidHandoff_Commits_SetsHashAndBranch_AppendsNotes_MovesToDone()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        const string expectedHash = "aabbccdd11223344aabbccdd11223344aabbccdd";
        const string handoffJson = """
            {
                "commit_body_bullets": ["Add handoff consumption"],
                "touched_files": ["src/RunBatchCommandHandler.cs"],
                "notes": "### Agent notes\n\n#### Summary\nImplemented the thing."
            }
            """;

        var git = GitAlwaysClean(expectedHash);
        var claude = ClaudeSucceedsWithHandoff(handoffJson);

        await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.CommitHash.Should().Be(expectedHash);
        saved.BranchName.Should().Be(batch.BranchName);
        saved.LaneName.Should().Be(SystemLaneNames.Done);
        saved.Description.Should().Contain("### Agent notes");
    }

    [Fact]
    public async Task ValidHandoff_CommitSubjectUsesTagToPrefix()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        const string handoffJson = """{"commit_body_bullets":["add X"],"touched_files":[],"notes":null}""";

        var git = GitAlwaysClean();
        var claude = ClaudeSucceedsWithHandoff(handoffJson);

        await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await git.Received(1).CommitAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.StartsWith("chore:") && msg.Contains($"(card #{card.Number})")),
            Arg.Any<CancellationToken>());
    }

    // ── missing handoff ────────────────────────────────────────────────────────

    [Fact]
    public async Task MissingHandoff_AfterExitZero_TreatedAsFailure_BatchHalts()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = GitAlwaysClean();
        var claude = ClaudeSucceedsNoHandoff();

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.CardFailure);
        result.Succeeded.Should().Be(0);

        await git.Received(1).ResetHardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── malformed handoff ──────────────────────────────────────────────────────

    [Fact]
    public async Task MalformedHandoff_TreatedAsFailure()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        const string malformedJson = "this is not json { at all";
        var git = GitAlwaysClean();
        var claude = ClaudeSucceedsWithHandoff(malformedJson);

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.CardFailure);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MalformedHandoff_FileIsDeletedAfterProcessing()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        const string malformedJson = "not valid json";
        var claude = ClaudeSucceedsWithHandoff(malformedJson);

        await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var handoffPath = Path.Combine(_worktreePath, ".bishop", "handoff.json");
        File.Exists(handoffPath).Should().BeFalse();
    }

    // ── commit failure ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitFailure_ResetsWorktree_RecordsFailure_HaltsBatch()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        const string handoffJson = """{"commit_body_bullets":["something"],"touched_files":[],"notes":null}""";

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("nothing to commit")));

        var claude = ClaudeSucceedsWithHandoff(handoffJson);

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.CardFailure);
        result.Succeeded.Should().Be(0);

        await git.Received(1).ResetHardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── notes: null ────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotesNull_CardMovesToDone_NoDescriptionAppended()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        const string handoffJson = """{"commit_body_bullets":["change"],"touched_files":[],"notes":null}""";
        var claude = ClaudeSucceedsWithHandoff(handoffJson);

        await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LaneName.Should().Be(SystemLaneNames.Done);
        saved.Description.Should().BeNullOrEmpty();
    }

    // ── multi-card ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultiCard_EachTransitions_ToDoing_ToDone_InOrder()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        const string handoffJson = """{"commit_body_bullets":["change"],"touched_files":[],"notes":null}""";
        var claude = ClaudeSucceedsWithHandoff(handoffJson);

        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(2);

        var savedC1 = await _db.Cards.SingleAsync(c => c.Id == c1.Id);
        var savedC2 = await _db.Cards.SingleAsync(c => c.Id == c2.Id);
        savedC1.LaneName.Should().Be(SystemLaneNames.Done);
        savedC2.LaneName.Should().Be(SystemLaneNames.Done);
    }

    [Fact]
    public async Task MultiCard_HandoffDeletedBetweenCards_EachCardWritesFresh()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        const string handoffJson = """{"commit_body_bullets":["change"],"touched_files":[],"notes":null}""";
        var claude = ClaudeSucceedsWithHandoff(handoffJson);

        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.Succeeded.Should().Be(2);

        var handoffPath = Path.Combine(_worktreePath, ".bishop", "handoff.json");
        File.Exists(handoffPath).Should().BeFalse("handoff.json should be deleted after the last card");
    }
}
