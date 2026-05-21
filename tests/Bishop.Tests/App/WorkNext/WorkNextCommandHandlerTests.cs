using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.MoveCard;
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
using NSubstitute;

namespace Bishop.Tests.App.WorkNext;

public sealed class WorkNextCommandHandlerTests : IClassFixture<DbFixture>
{
    private const string WorkspacePath = @"C:\fake\workspace";

    private readonly BishopDbContext _db;
    private readonly SqliteConnection _connection;

    public WorkNextCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _connection = fixture.Connection;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<Lane> lanes)> CreateWorkspaceWithLanesAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler(_db)
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ClaimCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new ClaimCardCommandHandler(_db, sender)
                .Handle(call.ArgAt<ClaimCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_db, sender)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetCardQueryHandler(_db)
                .Handle(call.ArgAt<GetCardQuery>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private static IGitCli GitAlwaysClean()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorkingTreeStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWorkingTreeStatusResult.Clean());
        return git;
    }

    private static IClaudeCliRunner ClaudeAlwaysSucceeds()
    {
        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(0);
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
        var add = new AddCardCommandHandler(_db);
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
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CapReached_StopsAtMaxIterationsEvenWhenMoreCardsRemain()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_db);
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
        await claude.Received(2).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeNonZeroExit_StopsAndSurfacesFailedCardNumber()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_db);
        var first = await add.Handle(new AddCardCommand(todo.Id, "T2", TagNames: ["test"]), default);
        // Inserted second → ends up at top (position 1) under insert-at-top semantics
        var second = await add.Handle(new AddCardCommand(todo.Id, "T1", TagNames: ["test"]), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(7);
        var handler = new WorkNextCommandHandler(GitAlwaysClean(), CreateSender(), claude);

        // Act
        var result = await handler.Handle(
            new WorkNextCommand(workspace.Id, WorkspacePath, "test", 10),
            default);

        // Assert
        result.CardsProcessed.Should().Be(0);
        result.StopReason.Should().Be(WorkNextStopReason.ClaudeFailed);
        result.FailedCardNumber.Should().Be(second.Number);
        await claude.Received(1).RunPromptAsync(WorkspacePath, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DirtyWorkingTree_AbortsBeforeAnyClaimOrClaude()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_db);
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
        await claude.DidNotReceive().RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaudeReceivesPromptWithCardNumber()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_db);
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
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessfulIteration_WritesBannerAndExitZero()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_db);
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
        lines.Should().Contain($"== Card #{card.Number}: Pretty Title ==");
        lines.Should().Contain("exit 0");
    }

    [Fact]
    public async Task ClaudeNonZeroExit_WritesExitLineWithExitCode()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_db);
        var card = await add.Handle(new AddCardCommand(todo.Id, "Broken", TagNames: ["test"]), default);

        var claude = Substitute.For<IClaudeCliRunner>();
        claude.RunPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(7);
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
        lines.Should().Contain($"== Card #{card.Number}: Broken ==");
        lines.Should().Contain("exit 7");
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
}
