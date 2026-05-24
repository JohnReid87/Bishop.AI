using System.Diagnostics;
using System.Text.Json;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Services.Claude;
using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Skills.GetSkillBootstrapInfo;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.App.WorkNext;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.WorkNext;

public sealed class WorkNextCommandHandlerTests : IClassFixture<DbFixture>, IDisposable
{
    private readonly string WorkspacePath;
    private readonly string BishopDir;
    private readonly string RunningFile;
    private readonly string StopFile;

    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public WorkNextCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _connection = fixture.Connection;

        WorkspacePath = Path.Combine(Path.GetTempPath(), "bishop-worknext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkspacePath);
        BishopDir = Path.Combine(WorkspacePath, ".bishop");
        RunningFile = Path.Combine(BishopDir, "worknext.running");
        StopFile = Path.Combine(BishopDir, "worknext.stop");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkspacePath))
                Directory.Delete(WorkspacePath, recursive: true);
        }
        catch
        {
        }
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceWithLanesAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ClaimCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new ClaimCardCommandHandler(_factory, sender)
                .Handle(call.ArgAt<ClaimCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_factory, sender)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetCardQueryHandler(_factory)
                .Handle(call.ArgAt<GetCardQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<RecordClaudeRunCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new RecordClaudeRunCommandHandler(_factory)
                .Handle(call.ArgAt<RecordClaudeRunCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<RecordAutoRunFailureCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new RecordAutoRunFailureCommandHandler(_factory)
                .Handle(call.ArgAt<RecordAutoRunFailureCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListLanesByWorkspaceQueryHandler()
                .Handle(call.ArgAt<ListLanesByWorkspaceQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<ListTagsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListTagsByWorkspaceQueryHandler()
                .Handle(call.ArgAt<ListTagsByWorkspaceQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetSkillBootstrapInfoQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetSkillBootstrapInfoQueryHandler(_factory, sender)
                .Handle(call.ArgAt<GetSkillBootstrapInfoQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetCardByNumberQueryHandler(_factory)
                .Handle(call.ArgAt<GetCardByNumberQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<SetCardCommitCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new SetCardCommitCommandHandler(_factory)
                .Handle(call.ArgAt<SetCardCommitCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new UpdateCardCommandHandler(_factory, sender)
                .Handle(call.ArgAt<UpdateCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private static IGitCli GitAlwaysClean()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.Success(new List<CommitInfo>(), null));
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("abc1234567890def1234567890abc1234567890ab");
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("main");
        return git;
    }

    private static void WriteHandoff(string bishopDir, string? notes = null)
    {
        Directory.CreateDirectory(bishopDir);
        var payload = new
        {
            commit_body_bullets = new[] { "Implemented card" },
            touched_files = Array.Empty<string>(),
            notes,
        };
        File.WriteAllText(
            Path.Combine(bishopDir, "handoff.json"),
            JsonSerializer.Serialize(payload));
    }

    private static IClaudeCliRunner ClaudeAlwaysSucceeds(ClaudeRunTotals? totals = null, int toolUseCount = 0)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var workspacePath = call.ArgAt<string>(0);
                WriteHandoff(Path.Combine(workspacePath, ".bishop"));
                return Task.FromResult(new ClaudeRunResult(0, totals, toolUseCount));
            });
        return claude;
    }

    private static IClaudeCliRunner ClaudeReturnsExitCode(int exitCode)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(exitCode, null, 0));
        return claude;
    }

    [Fact]
    public async Task EmptyLane_StopsCleanlyWithZeroCardsProcessed()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        result.FailedCardNumbers.Should().BeNull();
    }

    [Fact]
    public async Task ProcessesAllMatchingCards_UntilLaneExhausted()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T2", TagName: "test"), default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Plain"), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act — uncapped (max = 0) so the loop runs until claim returns null
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 0),
            default);

        // Assert
        result.Succeeded.Should().Be(2);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CapReached_StopsAtMaxIterationsEvenWhenMoreCardsRemain()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T2", TagName: "test"), default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T3", TagName: "test"), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 2),
            default);

        // Assert
        result.Succeeded.Should().Be(2);
        result.StopReason.Should().Be(WorkNextStopReason.CapReached);
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeNonZeroExit_SkipsCardAndContinuesToNextCard()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var first = await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "T2", TagName: "test"), default);
        // Inserted second → ends up at top (position 1) under insert-at-top semantics
        var second = await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "test"), default);

        var callCount = 0;
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new ClaudeRunResult(7, null, 0)); // second card (claimed first) fails
                WriteHandoff(Path.Combine(call.ArgAt<string>(0), ".bishop"));
                return Task.FromResult(new ClaudeRunResult(0, null, 0)); // first card (claimed second) succeeds
            });
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert — loop continues after the failure and processes the second card
        result.Succeeded.Should().Be(1);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        result.FailedCardNumbers.Should().Equal(second.Number);
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeNonZeroExit_PopulatesFailedCardNumbers()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "test"), default);

        var claude = ClaudeReturnsExitCode(7);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        result.FailedCardNumbers.Should().Equal(card.Number);
    }

    [Fact]
    public async Task ClaudeNonZeroExit_ResetsAndCleansWorkingTree()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "test"), default);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        var claude = ClaudeReturnsExitCode(7);
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        await git.Received(1).ResetHardAsync(WorkspacePath, Arg.Any<CancellationToken>());
        await git.Received(1).CleanWorkingTreeAsync(WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DirtyWorkingTree_AbortsBeforeAnyClaimOrClaude()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Dirty(["src/foo.cs", "README.md"]));
        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.DirtyWorkingTree);
        result.DirtyPaths.Should().Equal("src/foo.cs", "README.md");
        result.FailedCardNumbers.Should().BeNull();
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeReceivesPromptWithContextBlockAndCardNumber()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
            default);

        // Assert — prompt starts with the pre-stuffed context block and ends with the skill invocation
        await claude.Received(1).RunPromptAsync(
            WorkspacePath,
            Arg.Is<string>(p =>
                p.StartsWith("<bishop-context>") &&
                p.Contains($"\"number\": {card.Number}") &&
                p.EndsWith($"/bish-auto-card #{card.Number}")),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessfulIteration_WritesBannerAndExitZero()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Pretty Title", TagName: "test"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        var lines = output.ToString().Split(Environment.NewLine);
        lines.Should().Contain(l => l.EndsWith($"Card #{card.Number}: Pretty Title =="));
        lines.Should().Contain("exit 0");
    }

    [Fact]
    public async Task ClaudeNonZeroExit_WritesExitLineWithExitCode()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Broken", TagName: "test"), default);

        var claude = ClaudeReturnsExitCode(7);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        var lines = output.ToString().Split(Environment.NewLine);
        lines.Should().Contain(l => l.EndsWith($"Card #{card.Number}: Broken =="));
        lines.Should().Contain("exit 7");
    }

    [Fact]
    public async Task SuccessfulRun_AccumulatesClaudeTotalsOntoCard()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Accum", TagName: "test"), default);

        var claude = ClaudeAlwaysSucceeds(new ClaudeRunTotals(1000, 250));
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.TotalInputTokens.Should().Be(1000);
        saved.TotalOutputTokens.Should().Be(250);
        saved.ClaudeRunCount.Should().Be(1);
    }

    [Fact]
    public async Task SuccessfulRun_WithNullTotals_StillIncrementsRunCount()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"NoTotals", TagName: "test"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds(totals: null));

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.TotalInputTokens.Should().Be(0);
        saved.TotalOutputTokens.Should().Be(0);
        saved.ClaudeRunCount.Should().Be(1);
    }

    [Fact]
    public async Task ClaudeFailedRun_DoesNotAccumulateAnyTotals()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Fails", TagName: "test"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeReturnsExitCode(7));

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.ClaudeRunCount.Should().Be(0);
    }

    [Fact]
    public async Task SuccessfulIteration_WritesPerCardSummaryLine_AfterExit()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Summarised", TagName: "test"), default);

        var claude = ClaudeAlwaysSucceeds(new ClaudeRunTotals(12300, 4100), toolUseCount: 14);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        var lines = output.ToString().Split(Environment.NewLine);
        var exitIdx = Array.IndexOf(lines, "exit 0");
        exitIdx.Should().BeGreaterThan(-1);
        var summary = lines[exitIdx + 1];
        summary.Should().StartWith($"card #{card.Number}: 14 tool uses, 12.3k↑ 4.1k↓ in ");
        summary.Should().Contain("(git ");
        summary.Should().Contain("· claim ");
        summary.Should().Contain("· claude ");
        summary.Should().Contain("· record ");
    }

    [Fact]
    public async Task FailedIteration_StillWritesPerCardSummaryLine()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Broken", TagName: "test"), default);

        var claude = ClaudeReturnsExitCode(7);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        var lines = output.ToString().Split(Environment.NewLine);
        var exitIdx = Array.IndexOf(lines, "exit 7");
        exitIdx.Should().BeGreaterThan(-1);
        var summary = lines[exitIdx + 1];
        summary.Should().StartWith($"card #{card.Number}: 0 tool uses, 0↑ 0↓ in ");
        summary.Should().Contain("(git ");
        summary.Should().Contain("· claim ");
        summary.Should().Contain("· claude ");
        summary.Should().Contain("· record ");
    }

    [Fact]
    public async Task EmptyLane_WritesNothingToStdout()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task NotAGitRepo_AbortsBeforeAnyClaimOrClaude()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.NotAGitRepo());
        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.NotAGitRepo);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GitNotFound_AbortsBeforeAnyClaimOrClaude()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.GitNotFound());
        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.GitNotFound);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunPromptAsync_Throws_ExceptionPropagates()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ClaudeRunResult>(new InvalidOperationException("runner failed")));
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act & Assert
        Func<Task> act = () => handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("runner failed");
    }

    [Fact]
    public async Task ClaimCardSender_Throws_ExceptionPropagates()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();

        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ClaimCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Card?>(new InvalidOperationException("db error")));
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), sender, ClaudeAlwaysSucceeds());

        // Act & Assert
        Func<Task> act = () => handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("db error");
    }

    [Fact]
    public async Task Model_IsThreadedToClaudeRunner()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1, "claude-sonnet-4-6"),
            default);

        // Assert
        await claude.Received(1).RunPromptAsync(
            WorkspacePath,
            Arg.Any<string>(),
            "claude-sonnet-4-6",
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartLine_IncludesModelBracket_WhenModelIsSet()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name,"My Card", TagName: "test"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1, "claude-sonnet-4-6"),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        output.ToString().Split(Environment.NewLine)
            .Should().Contain(l => l.EndsWith($"Card #{card.Number}: My Card  [claude-sonnet-4-6] =="));
    }

    [Fact]
    public async Task StartLine_ExcludesModelBracket_WhenModelIsNull()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name,"My Card", TagName: "test"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            await handler.Handle(
                new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
                default);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert — start line ends with the title and ==, with no model bracket between them.
        var lines = output.ToString().Split(Environment.NewLine);
        lines.Should().ContainSingle(l => l.EndsWith($"Card #{card.Number}: My Card =="));
    }

    [Fact]
    public async Task StopFileDroppedMidRun_ExitsWithCancelledAfterCurrentCard()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T2", TagName: "test"), default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                // Drop a stop file mid-run, after the first card finishes.
                File.WriteAllText(StopFile, "");
                WriteHandoff(Path.Combine(call.ArgAt<string>(0), ".bishop"));
                return Task.FromResult(new ClaudeRunResult(0, null, 0));
            });
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.Cancelled);
        result.Succeeded.Should().Be(1);
        File.Exists(StopFile).Should().BeFalse();
        await claude.Received(1).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreExistingStopFile_IsDeletedOnEntry_AndDoesNotPreCancelRun()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        Directory.CreateDirectory(BishopDir);
        File.WriteAllText(StopFile, "");

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
            default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.CapReached);
        result.Succeeded.Should().Be(1);
        File.Exists(StopFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_ExistsDuringRun_AndIsDeletedOnExit()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var heartbeatExistedDuringRun = false;
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                heartbeatExistedDuringRun = File.Exists(RunningFile);
                WriteHandoff(Path.Combine(call.ArgAt<string>(0), ".bishop"));
                return Task.FromResult(new ClaudeRunResult(0, null, 0));
            });
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
            default);

        // Assert
        heartbeatExistedDuringRun.Should().BeTrue();
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_ContainsCurrentPidAndUtcStartTimestamp()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        string? captured = null;
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = File.ReadAllText(RunningFile);
                WriteHandoff(Path.Combine(call.ArgAt<string>(0), ".bishop"));
                return Task.FromResult(new ClaudeRunResult(0, null, 0));
            });
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
            default);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        // Assert
        captured.Should().NotBeNullOrEmpty();
        var lines = captured!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
        int.Parse(lines[0]).Should().Be(Environment.ProcessId);
        var parsed = DateTimeOffset.Parse(lines[1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        parsed.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        parsed.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_EmptyLane()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_CapReached()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1), default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.CapReached);
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_ClaudeFailed()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "test"), default);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeReturnsExitCode(7));

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert — loop skips the failed card and exits via EmptyLane; heartbeat is still cleaned up
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_DirtyWorkingTree()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Dirty(["src/foo.cs"]));
        var handler = new WorkNextCommandHandler(git, CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.DirtyWorkingTree);
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_NotAGitRepo()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.NotAGitRepo());
        var handler = new WorkNextCommandHandler(git, CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.NotAGitRepo);
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_GitNotFound()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.GitNotFound());
        var handler = new WorkNextCommandHandler(git, CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.GitNotFound);
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_Cancelled()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T2", TagName: "test"), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                File.WriteAllText(StopFile, "");
                WriteHandoff(Path.Combine(call.ArgAt<string>(0), ".bishop"));
                return Task.FromResult(new ClaudeRunResult(0, null, 0));
            });
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        result.StopReason.Should().Be(WorkNextStopReason.Cancelled);
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_DeletedOnExit_UnhandledException()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ClaudeRunResult>(new InvalidOperationException("boom")));
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        Func<Task> act = () => handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(RunningFile).Should().BeFalse();
    }

    [Fact]
    public async Task DoesNotFalsePositiveDirtyTreeOnOwnRuntimeFiles()
    {
        // Arrange — real git repo with .bishop/ gitignored, so the handler's own
        // runtime files (worknext.running) don't trigger DirtyWorkingTree
        var repoPath = Path.Combine(Path.GetTempPath(), "bishop-gitignore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoPath);
        try
        {
            RunGit(repoPath, ["init"]);
            RunGit(repoPath, ["config", "user.email", "test@test.com"]);
            RunGit(repoPath, ["config", "user.name", "Test"]);
            File.WriteAllText(Path.Combine(repoPath, ".gitignore"), ".bishop/\n");
            RunGit(repoPath, ["add", ".gitignore"]);
            RunGit(repoPath, ["commit", "-m", "init"]);

            var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
            var todo = lanes.Single(l => l.Name == "To Do");
            await new AddCardCommandHandler(_factory)
                .Handle(new AddCardCommand(workspace.Id, todo.Name,"T1", TagName: "test"), default);

            var handler = new WorkNextCommandHandler(new GitCli(), CreateSender(), ClaudeAlwaysSucceeds());

            // Act
            var result = await handler.Handle(
                new WorkNextCommand(workspace.Id, repoPath, "test", 1),
                default);

            // Assert
            result.StopReason.Should().NotBe(WorkNextStopReason.DirtyWorkingTree);
        }
        finally
        {
            try { Directory.Delete(repoPath, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClaudeNonZeroExit_PersistsLastAutoRunFailedAt()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "test"), default);

        var before = DateTimeOffset.UtcNow;
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeReturnsExitCode(7));

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10), default);

        // Assert
        var after = DateTimeOffset.UtcNow;
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
        saved.LastAutoRunFailedAt!.Value.Should().BeOnOrAfter(before);
        saved.LastAutoRunFailedAt!.Value.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task ClaudeSuccessfulRun_DoesNotSetLastAutoRunFailedAt()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "test"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().BeNull();
    }

    [Fact]
    public async Task HandoffJsonMissing_AfterClaudeExitZero_TreatsAsFailure()
    {
        // Arrange — claude returns 0 but writes no handoff.json
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature"), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClaudeRunResult(0, null, 0)));
        var git = GitAlwaysClean();
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 10), default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.FailedCardNumbers.Should().Equal(card.Number);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        await git.Received(1).ResetHardAsync(WorkspacePath, Arg.Any<CancellationToken>());
        await git.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandoffJsonMalformed_AfterClaudeExitZero_TreatsAsFailure()
    {
        // Arrange — claude returns 0 but writes garbage JSON
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature"), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var bishopDir = Path.Combine(call.ArgAt<string>(0), ".bishop");
                Directory.CreateDirectory(bishopDir);
                File.WriteAllText(Path.Combine(bishopDir, "handoff.json"), "not valid json {{");
                return Task.FromResult(new ClaudeRunResult(0, null, 0));
            });
        var git = GitAlwaysClean();
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 10), default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.FailedCardNumbers.Should().Equal(card.Number);
        await git.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidHandoffJson_CommitFails_TreatsAsFailure()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature"), default);

        var claude = ClaudeAlwaysSucceeds();
        var git = GitAlwaysClean();
        git.CommitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("nothing to commit")));
        var handler = new WorkNextCommandHandler(git, CreateSender(), claude);

        // Act
        var result = await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 10), default);

        // Assert
        result.Succeeded.Should().Be(0);
        result.FailedCardNumbers.Should().Equal(card.Number);
        await git.Received(1).ResetHardAsync(WorkspacePath, Arg.Any<CancellationToken>());
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidHandoffJson_SuccessfulRun_MovesCardToDone()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.LaneName.Should().Be("Done");
        saved.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidHandoffJson_SuccessfulRun_RecordsCommitHashAndBranchOnCard()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature"), default);

        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.CommitHash.Should().Be("abc1234567890def1234567890abc1234567890ab");
        saved.BranchName.Should().Be("main");
    }

    [Fact]
    public async Task ValidHandoffJson_WithNotes_AppendsNotesToCardDescription()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature", Description: "### Why\nOriginal."), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                WriteHandoff(Path.Combine(call.ArgAt<string>(0), ".bishop"), notes: "### Agent notes\n\nDid the thing.");
                return Task.FromResult(new ClaudeRunResult(0, null, 0));
            });
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 1), default);

        // Assert
        var saved = await _db.Cards.SingleAsync(c => c.Id == card.Id);
        saved.Description.Should().Contain("### Why\nOriginal.");
        saved.Description.Should().Contain("### Agent notes");
        saved.Description.Should().Contain("Did the thing.");
    }

    [Fact]
    public async Task ValidHandoffJson_HandoffFileDeletedAfterProcessing()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "T1", TagName: "feature"), default);

        var handoffPath = Path.Combine(BishopDir, "handoff.json");
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 1), default);

        // Assert
        File.Exists(handoffPath).Should().BeFalse();
    }

    [Fact]
    public async Task CommitMessage_UsesTagPrefixAndCardTitle()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(workspace.Id, todo.Name, "My Feature", TagName: "feature"), default);

        var git = GitAlwaysClean();
        var handler = new WorkNextCommandHandler(git, CreateSender(), ClaudeAlwaysSucceeds());

        // Act
        await handler.Handle(new WorkNextCommand(workspace.Id, WorkspacePath, "feature", 1), default);

        // Assert
        await git.Received(1).CommitAsync(
            WorkspacePath,
            Arg.Is<string>(m => m.StartsWith($"feat: My Feature (card {card.Number})")),
            Arg.Any<CancellationToken>());
    }

    private static void RunGit(string workingDir, string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
    }
}
