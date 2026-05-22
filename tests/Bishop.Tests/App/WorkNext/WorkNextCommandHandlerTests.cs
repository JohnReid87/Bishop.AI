using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Claude;
using Bishop.App.Git;
using Bishop.App.Lanes.ListLanesByWorkspace;
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

public sealed class WorkNextCommandHandlerTests : IClassFixture<DbFixture>
{
    private const string WorkspacePath = @"C:\fake\workspace";

    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public WorkNextCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _connection = fixture.Connection;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<Lane> lanes)> CreateWorkspaceWithLanesAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler(_factory)
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
        return sender;
    }

    private static IGitCli GitAlwaysClean()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        return git;
    }

    private static IClaudeCliRunner ClaudeAlwaysSucceeds(ClaudeRunTotals? totals = null, int toolUseCount = 0)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeRunResult(0, totals, toolUseCount));
        return claude;
    }

    private static IClaudeCliRunner ClaudeReturnsExitCode(int exitCode)
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
        result.CardsProcessed.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        result.FailedCardNumber.Should().BeNull();
    }

    [Fact]
    public async Task ProcessesAllMatchingCards_UntilLaneExhausted()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);
        await add.Handle(new AddCardCommand(todo.Id, "T2", TagNames: ["test"]), default);
        await add.Handle(new AddCardCommand(todo.Id, "Plain"), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act — uncapped (max = 0) so the loop runs until claim returns null
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 0),
            default);

        // Assert
        result.CardsProcessed.Should().Be(2);
        result.StopReason.Should().Be(WorkNextStopReason.EmptyLane);
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CapReached_StopsAtMaxIterationsEvenWhenMoreCardsRemain()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);
        await add.Handle(new AddCardCommand(todo.Id, "T2", TagNames: ["test"]), default);
        await add.Handle(new AddCardCommand(todo.Id, "T3", TagNames: ["test"]), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 2),
            default);

        // Assert
        result.CardsProcessed.Should().Be(2);
        result.StopReason.Should().Be(WorkNextStopReason.CapReached);
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeNonZeroExit_StopsAndSurfacesFailedCardNumber()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var first = await add.Handle(new AddCardCommand(todo.Id, "T2", TagNames: ["test"]), default);
        // Inserted second → ends up at top (position 1) under insert-at-top semantics
        var second = await add.Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

        var claude = ClaudeReturnsExitCode(7);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.CardsProcessed.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.ClaudeFailed);
        result.FailedCardNumber.Should().Be(second.Number);
        await claude.Received(1).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DirtyWorkingTree_AbortsBeforeAnyClaimOrClaude()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

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
        result.CardsProcessed.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.DirtyWorkingTree);
        result.DirtyPaths.Should().Equal("src/foo.cs", "README.md");
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeReceivesPromptWithCardNumber()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

        var claude = ClaudeAlwaysSucceeds();
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 1),
            default);

        // Assert
        await claude.Received(1).RunPromptAsync(
            WorkspacePath,
            $"/bish-auto-card #{card.Number}",
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessfulIteration_WritesBannerAndExitZero()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(todo.Id, "Pretty Title", TagNames: ["test"]), default);

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
        var card = await add.Handle(new AddCardCommand(todo.Id, "Broken", TagNames: ["test"]), default);

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
        var card = await add.Handle(new AddCardCommand(todo.Id, "Accum", TagNames: ["test"]), default);

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
        var card = await add.Handle(new AddCardCommand(todo.Id, "NoTotals", TagNames: ["test"]), default);

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
        var card = await add.Handle(new AddCardCommand(todo.Id, "Fails", TagNames: ["test"]), default);

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
        var card = await add.Handle(new AddCardCommand(todo.Id, "Summarised", TagNames: ["test"]), default);

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
    }

    [Fact]
    public async Task FailedIteration_StillWritesPerCardSummaryLine()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(todo.Id, "Broken", TagNames: ["test"]), default);

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
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

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
        result.CardsProcessed.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.NotAGitRepo);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GitNotFound_AbortsBeforeAnyClaimOrClaude()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

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
        result.CardsProcessed.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.GitNotFound);
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunPromptAsync_Throws_ExceptionPropagates()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
        await new AddCardCommandHandler(_factory).Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

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
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartLine_IncludesModelBracket_WhenModelIsSet()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory).Handle(
            new AddCardCommand(todo.Id, "My Card", TagNames: ["test"]), default);

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
            new AddCardCommand(todo.Id, "My Card", TagNames: ["test"]), default);

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
}
