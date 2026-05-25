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
using Bishop.App.Services.GitHub;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class RunBatchCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly string _worktreePath;

    private const string ValidHandoffJson =
        """{"commit_body_bullets":["test change"],"touched_files":[],"notes":null}""";

    public RunBatchCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _worktreePath = Path.Combine(Path.GetTempPath(), "bishop-test-" + Guid.NewGuid().ToString("N")[..8]);
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

    private ISender CreateSender() => new BatchTestSender(_factory);

    private sealed class BatchTestSender : ISender
    {
        private readonly MoveCardCommandHandler _moveCard;
        private readonly RecordClaudeRunCommandHandler _recordClaudeRun;
        private readonly RecordAutoRunFailureCommandHandler _recordAutoRunFailure;
        private readonly SetCardCommitCommandHandler _setCardCommit;
        private readonly GetWorkspaceQueryHandler _getWorkspace;
        private readonly UpdateCardCommandHandler _updateCard;

        public BatchTestSender(IDbContextFactory<BishopDbContext> factory)
        {
            _moveCard = new(factory, Substitute.For<IGhCli>(), NullLogger<MoveCardCommandHandler>.Instance);
            _recordClaudeRun = new(factory);
            _recordAutoRunFailure = new(factory);
            _setCardCommit = new(factory);
            _getWorkspace = new(factory);
            _updateCard = new(factory, this);
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is MoveCardCommand cmd1)
                return (TResponse)(object)(await _moveCard.Handle(cmd1, ct));
            if (request is SetCardCommitCommand cmd4)
                return (TResponse)(object)(await _setCardCommit.Handle(cmd4, ct));
            if (request is UpdateCardCommand cmd5)
                return (TResponse)(object)(await _updateCard.Handle(cmd5, ct));
            if (request is GetWorkspaceQuery cmd6)
            {
                var ws = await _getWorkspace.Handle(cmd6, ct);
                if (ws is null) return default!;
                return (TResponse)(object)ws;
            }
            return default!;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            Task.FromResult<object?>(null);

        public async Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest
        {
            if (request is RecordClaudeRunCommand cmd1)
                await _recordClaudeRun.Handle(cmd1, ct);
            else if (request is RecordAutoRunFailureCommand cmd2)
                await _recordAutoRunFailure.Handle(cmd2, ct);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) =>
            AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) =>
            AsyncEnumerable.Empty<object?>();
    }

    private static IGitCli GitAlwaysClean()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("deadbeef12345678deadbeef12345678deadbeef");
        return git;
    }

    private IClaudeCliRunner ClaudeAlwaysSucceeds()
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var path = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                await File.WriteAllTextAsync(path, ValidHandoffJson);
                return new ClaudeRunResult(0, null, 0);
            });
        return claude;
    }

    private static IClaudeCliRunner ClaudeReturnsExitCode(int exitCode)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
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

    [Fact]
    public async Task DuplicateBatchName_Throws()
    {
        var repo = new BatchRepository(_factory);
        var name = U("batch");
        await repo.CreateAsync(name, $"bishop/{U("br")}", "main", _worktreePath);
        await repo.CreateAsync(name, $"bishop/{U("br")}", "main", _worktreePath);

        var handler = CreateHandler();

        Func<Task> act = () => handler.Handle(new RunBatchCommand(name, Resume: false), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Multiple batches*");
    }

    [Fact]
    public async Task WorkspaceNotFound_Throws()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns((Workspace?)null);

        var handler = CreateHandler(sender: sender);

        Func<Task> act = () => handler.Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Workspace not found*");
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyBatch_RemainsWorking_AfterFinishedRun()
    {
        var batch = await CreateBatchAsync();

        var result = await CreateHandler().Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(0);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        saved.ClosedReason.Should().BeNull();
    }

    [Fact]
    public async Task AllCardsSucceed_BatchRemainsWorking_CardsMovedToDone()
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
        saved.Status.Should().Be(BatchStatus.Working);

        var savedC1 = await _db.Cards.SingleAsync(c => c.Id == c1.Id);
        var savedC2 = await _db.Cards.SingleAsync(c => c.Id == c2.Id);
        savedC1.LaneName.Should().Be(SystemLaneNames.Done);
        savedC2.LaneName.Should().Be(SystemLaneNames.Done);
    }

    [Fact]
    public async Task FreshRun_TransitionsBatchToWorking_StaysWorkingOnSuccess()
    {
        var batch = await CreateBatchAsync();

        await CreateHandler().Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
        saved.ClosedReason.Should().BeNull();
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

        await CreateHandler(git: git, claude: ClaudeReturnsExitCode(1))
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        await git.Received(1).ResetHardAsync(_worktreePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(_worktreePath, Arg.Any<CancellationToken>());
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
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());

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
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
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
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
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
            _worktreePath,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int?>(),
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
            _worktreePath,
            Arg.Is<string>(p => p.Contains($"/bish-auto-card #{card.Number}")),
            Arg.Any<string>(),
            Arg.Any<int?>(),
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
            Arg.Any<int?>(),
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
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var path = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                await File.WriteAllTextAsync(path, ValidHandoffJson);
                return new ClaudeRunResult(0, new ClaudeRunTotals(1000, 250, 500, 100), 0);
            });

        await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.TotalInputTokens.Should().Be(1000);
        saved.TotalOutputTokens.Should().Be(250);
        saved.TotalCacheCreationTokens.Should().Be(500);
        saved.TotalCacheReadTokens.Should().Be(100);
        saved.ClaudeRunCount.Should().Be(1);
        saved.LaneName.Should().Be(SystemLaneNames.Done);
    }

    // ── commit recording ───────────────────────────────────────────────────────

    [Fact]
    public async Task SuccessfulCard_RecordsCommitHash_FromCommitAsync()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("abc1234567890abcdef");

        await CreateHandler(git: git).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.CommitHash.Should().Be("abc1234567890abcdef");
        saved.BranchName.Should().Be(batch.BranchName);
        saved.LaneName.Should().Be(SystemLaneNames.Done);
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
        await new MoveCardCommandHandler(_factory, Substitute.For<IGhCli>(), NullLogger<MoveCardCommandHandler>.Instance)
            .Handle(new MoveCardCommand(c1.Id, SystemLaneNames.Done, 0, KeepOpen: true), default);

        var claude = ClaudeAlwaysSucceeds();
        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: true), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(1);

        await claude.Received(1).RunPromptAsync(
            _worktreePath,
            Arg.Is<string>(p => p.Contains($"/bish-auto-card #{c2.Number}")),
            Arg.Any<string>(),
            Arg.Any<int?>(),
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
        await new MoveCardCommandHandler(_factory, Substitute.For<IGhCli>(), NullLogger<MoveCardCommandHandler>.Instance)
            .Handle(new MoveCardCommand(card.Id, SystemLaneNames.Done, 0, KeepOpen: true), default);

        var claude = ClaudeAlwaysSucceeds();
        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: true), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(0);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());

        var saved = await _db.Batches.SingleAsync(b => b.Id == batch.Id);
        saved.Status.Should().Be(BatchStatus.Working);
    }

    // ── handoff missing ────────────────────────────────────────────────────────

    [Fact]
    public async Task ClaudeSucceeds_HandoffAbsent_ResetsAndStopsWithHandoffMissing()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = GitAlwaysClean();
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(0, null, 0));

        var result = await CreateHandler(git: git, claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.HandoffMissing);
        result.FailedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);
        await git.Received(1).ResetHardAsync(_worktreePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(_worktreePath, Arg.Any<CancellationToken>());
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ClaudeSucceeds_HandoffMalformed_ResetsAndStopsWithHandoffMissing()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var path = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                await File.WriteAllTextAsync(path, "null");
                return new ClaudeRunResult(0, null, 0);
            });

        var result = await CreateHandler(claude: claude)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.HandoffMissing);
        result.FailedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── commit failure ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitAsyncThrows_ResetsAndStopsWithCardFailure()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("nothing to commit")));

        var result = await CreateHandler(git: git)
            .Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.CardFailure);
        result.FailedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);
        await git.Received(1).ResetHardAsync(_worktreePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(_worktreePath, Arg.Any<CancellationToken>());
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── partial success ────────────────────────────────────────────────────────

    [Fact]
    public async Task PartialSuccess_FirstCardSucceeds_SecondFails_SucceededAndFailedBothSet()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        var callCount = 0;
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (++callCount == 1)
                {
                    var path = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                    await File.WriteAllTextAsync(path, ValidHandoffJson);
                    return new ClaudeRunResult(0, null, 0);
                }
                return new ClaudeRunResult(7, null, 0);
            });

        var result = await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.CardFailure);
        result.Succeeded.Should().Be(1);
        result.FailedCardNumbers.Should().ContainSingle().Which.Should().Be(c2.Number);
    }

    // ── TagToPrefix ────────────────────────────────────────────────────────────

    [Fact]
    public async Task NullTag_CommitMessageUsesChorePrefix()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        string? capturedMessage = null;
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMessage = call.ArgAt<string>(1);
                return Task.FromResult("deadbeef");
            });

        await CreateHandler(git: git).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        capturedMessage.Should().NotBeNull().And.StartWith("chore: ");
    }

    // ── handoff JSON parse failure ─────────────────────────────────────────────

    [Fact]
    public async Task HandoffJsonSyntaxError_TreatedAsHandoffMissing()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var path = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                await File.WriteAllTextAsync(path, "{{invalid json!");
                return new ClaudeRunResult(0, null, 0);
            });

        var result = await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.HandoffMissing);
        result.FailedCardNumbers.Should().ContainSingle().Which.Should().Be(card.Number);
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    // ── lock file refresh failure ──────────────────────────────────────────────

    [Fact]
    public async Task RefreshLockFailure_DoesNotAbortBatch()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var c1 = await AddCardAsync(workspace.Id, todo.Name);
        var c2 = await AddCardAsync(workspace.Id, todo.Name);
        var batch = await CreateBatchAsync(c1.Id, c2.Id);

        var lockPath = Path.Combine(_worktreePath, ".bishop", $"batch-{batch.Id}.lock");
        var gitCallCount = 0;

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Make the lock file read-only after card 1's git check so card 2's RefreshLockFile fails.
                if (++gitCallCount == 1 && File.Exists(lockPath))
                    File.SetAttributes(lockPath, FileAttributes.ReadOnly);
                return Task.FromResult<GetWorkingTreeStatusResult>(new GetWorkingTreeStatusResult.Clean());
            });
        git.StageAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("deadbeef");

        var result = await CreateHandler(git: git).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        result.StopReason.Should().Be(RunBatchStopReason.Finished);
        result.Succeeded.Should().Be(2);
        result.FailedCardNumbers.Should().BeNull();
    }

    // ── lock file lifecycle ────────────────────────────────────────────────────

    [Fact]
    public async Task LockFile_ExistsDuringRun_DeletedAfterFinish()
    {
        var (workspace, lanes) = await CreateWorkspaceAsync();
        var card = await AddCardAsync(workspace.Id, lanes.Single(l => l.Name == SystemLaneNames.ToDo).Name);
        var batch = await CreateBatchAsync(card.Id);

        var lockPath = Path.Combine(_worktreePath, ".bishop", $"batch-{batch.Id}.lock");
        var lockExistedDuringRun = false;

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                lockExistedDuringRun = File.Exists(lockPath);
                var handoffPath = Path.Combine(_worktreePath, ".bishop", "handoff.json");
                await File.WriteAllTextAsync(handoffPath, ValidHandoffJson);
                return new ClaudeRunResult(0, null, 0);
            });

        await CreateHandler(claude: claude).Handle(new RunBatchCommand(batch.Name, Resume: false), default);

        lockExistedDuringRun.Should().BeTrue("lock file should exist while a card is being processed");
        File.Exists(lockPath).Should().BeFalse("lock file should be deleted after the run finishes");
    }
}
