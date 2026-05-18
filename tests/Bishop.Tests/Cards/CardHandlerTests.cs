using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Cards;

public sealed class CardHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BishopDbContext _db;

    public CardHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new BishopDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<(Workspace workspace, IReadOnlyList<Lane> lanes)> CreateWorkspaceWithLanesAsync()
    {
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("Test", @"C:\test"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler(_db)
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    [Fact]
    public async Task AddCard_PersistsAndReturnsCard()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoLane = lanes[0];

        var handler = new AddCardCommandHandler(_db);
        var result = await handler.Handle(new AddCardCommand(todoLane.Id, "My Task", "Some details"), default);

        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("My Task");
        result.Description.Should().Be("Some details");
        result.LaneId.Should().Be(todoLane.Id);
        result.Position.Should().Be(1);
        result.CreatedAt.Should().NotBe(default);
        result.UpdatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task AddCard_AssignsSequentialPositions()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);

        var first = await add.Handle(new AddCardCommand(laneId, "First"), default);
        var second = await add.Handle(new AddCardCommand(laneId, "Second"), default);

        first.Position.Should().Be(1);
        second.Position.Should().Be(2);
    }

    [Fact]
    public async Task RemoveCard_DeletesCard()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "ToRemove"), default);

        await new RemoveCardCommandHandler(_db)
            .Handle(new RemoveCardCommand(card.Id), default);

        var remaining = await _db.Cards.FindAsync(card.Id);
        remaining.Should().BeNull();
    }

    [Fact]
    public async Task ListCardsByWorkspace_ReturnsCardsOrderedByLaneThenPosition()
    {
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_db);
        await add.Handle(new AddCardCommand(lanes[0].Id, "Todo-1"), default);
        await add.Handle(new AddCardCommand(lanes[0].Id, "Todo-2"), default);
        await add.Handle(new AddCardCommand(lanes[1].Id, "Doing-1"), default);

        var handler = new ListCardsByWorkspaceQueryHandler(_db);
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);

        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Todo-1");
        result[1].Title.Should().Be("Todo-2");
        result[2].Title.Should().Be("Doing-1");
    }

    [Fact]
    public async Task MoveCard_WithinLane_ReordersCorrectly()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(laneId, "A"), default);
        var b = await add.Handle(new AddCardCommand(laneId, "B"), default);
        var c = await add.Handle(new AddCardCommand(laneId, "C"), default);

        // Move C (pos 3) to position 1 → expected order: C, A, B
        var handler = new MoveCardCommandHandler(_db);
        await handler.Handle(new MoveCardCommand(c.Id, laneId, 1), default);

        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();

        cards.Select(x => x.Title).Should().Equal("C", "A", "B");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveCard_AcrossLanes_ReordersSourceAndTarget()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoId = lanes[0].Id;
        var doingId = lanes[1].Id;
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(todoId, "A"), default);
        var b = await add.Handle(new AddCardCommand(todoId, "B"), default);
        await add.Handle(new AddCardCommand(doingId, "X"), default);

        // Move A from To Do to Doing at position 1 → Doing: A(1), X(2); To Do: B(1)
        var handler = new MoveCardCommandHandler(_db);
        await handler.Handle(new MoveCardCommand(a.Id, doingId, 1), default);

        var todoCards = await _db.Cards
            .Where(x => x.LaneId == todoId)
            .OrderBy(x => x.Position)
            .ToListAsync();

        var doingCards = await _db.Cards
            .Where(x => x.LaneId == doingId)
            .OrderBy(x => x.Position)
            .ToListAsync();

        todoCards.Select(x => x.Title).Should().Equal("B");
        todoCards[0].Position.Should().Be(1);

        doingCards.Select(x => x.Title).Should().Equal("A", "X");
        doingCards.Select(x => x.Position).Should().Equal(1, 2);
    }

    [Fact]
    public async Task MoveCard_ToEndOfLane_PlacesLast()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(laneId, "A"), default);
        var b = await add.Handle(new AddCardCommand(laneId, "B"), default);
        var c = await add.Handle(new AddCardCommand(laneId, "C"), default);

        // Move A (pos 1) to position 3 (end) → expected order: B, C, A
        var handler = new MoveCardCommandHandler(_db);
        await handler.Handle(new MoveCardCommand(a.Id, laneId, 3), default);

        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();

        cards.Select(x => x.Title).Should().Equal("B", "C", "A");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }
}
