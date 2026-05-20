using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Cards;

public sealed class ClaimCardCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly SqliteConnection _connection;

    public ClaimCardCommandHandlerTests(DbFixture fixture)
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
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_db, sender)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetCardQueryHandler(_db)
                .Handle(call.ArgAt<GetCardQuery>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    [Fact]
    public async Task Claim_WithoutTag_PicksTopOfSourceLaneAndMovesToDoing()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var doing = lanes.Single(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_db);
        await add.Handle(new AddCardCommand(todo.Id, "First"), default);
        var second = await add.Handle(new AddCardCommand(todo.Id, "Second"), default);
        var handler = new ClaimCardCommandHandler(_db, CreateSender());

        // Act — Second was added last so insert-at-top puts it at position 1
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(second.Id);
        claimed.LaneId.Should().Be(doing.Id);
    }

    [Fact]
    public async Task Claim_WithTag_PicksFirstMatchingCardEvenWhenNotAtTop()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var doing = lanes.Single(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_db);

        // Inserted in this order — with insert-at-top, final lane order top→bottom is:
        // Plain-3 (pos 1), Tagged-test (pos 2), Plain-1 (pos 3)
        await add.Handle(new AddCardCommand(todo.Id, "Plain-1"), default);
        var tagged = await add.Handle(
            new AddCardCommand(todo.Id, "Tagged-test", TagNames: ["test"]),
            default);
        await add.Handle(new AddCardCommand(todo.Id, "Plain-3"), default);
        var handler = new ClaimCardCommandHandler(_db, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do", TagName: "test"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(tagged.Id);
        claimed.LaneId.Should().Be(doing.Id);
        claimed.CardTags.Should().ContainSingle().Which.Tag.Name.Should().Be("test");
    }

    [Fact]
    public async Task Claim_WithTag_NoMatchButOtherCardsPresent_ReturnsNullAndMovesNothing()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var doing = lanes.Single(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_db);
        await add.Handle(new AddCardCommand(todo.Id, "Plain-1"), default);
        await add.Handle(new AddCardCommand(todo.Id, "Tagged-bug", TagNames: ["bug"]), default);
        var handler = new ClaimCardCommandHandler(_db, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do", TagName: "test"),
            default);

        // Assert
        claimed.Should().BeNull();
        var todoCount = await _db.Cards.CountAsync(c => c.LaneId == todo.Id);
        var doingCount = await _db.Cards.CountAsync(c => c.LaneId == doing.Id);
        todoCount.Should().Be(2);
        doingCount.Should().Be(0);
    }

    [Fact]
    public async Task Claim_EmptySourceLane_ReturnsNull()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ClaimCardCommandHandler(_db, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert
        claimed.Should().BeNull();
    }

    [Fact]
    public async Task Claim_EmptySourceLane_WithTag_ReturnsNull()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ClaimCardCommandHandler(_db, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do", TagName: "test"),
            default);

        // Assert
        claimed.Should().BeNull();
    }

    [Fact]
    public async Task Claim_UnknownSourceLane_Throws()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ClaimCardCommandHandler(_db, CreateSender());

        // Act
        var act = async () => await handler.Handle(
            new ClaimCardCommand(workspace.Id, "Nope"),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Lane 'Nope' not found in workspace.");
    }
}
