using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class CardHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly SqliteConnection _connection;

    public CardHandlerTests(DbFixture fixture)
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

    [Fact]
    public async Task AddCard_PersistsAndReturnsCard()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoLane = lanes[0];
        var handler = new AddCardCommandHandler(_db);

        // Act
        var result = await handler.Handle(new AddCardCommand(todoLane.Id, "My Task", "Some details"), default);

        // Assert
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
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);

        // Act
        var first = await add.Handle(new AddCardCommand(laneId, "First"), default);
        var second = await add.Handle(new AddCardCommand(laneId, "Second"), default);

        // Assert
        first.Position.Should().Be(1);
        second.Position.Should().Be(2);
    }

    [Fact]
    public async Task RemoveCard_DeletesCard()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "ToRemove"), default);

        // Act
        await new RemoveCardCommandHandler(_db)
            .Handle(new RemoveCardCommand(card.Id), default);

        // Assert
        var remaining = await _db.Cards.FindAsync(card.Id);
        remaining.Should().BeNull();
    }

    [Fact]
    public async Task ListCardsByWorkspace_ReturnsCardsOrderedByLaneThenPosition()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_db);
        await add.Handle(new AddCardCommand(lanes[0].Id, "Todo-1"), default);
        await add.Handle(new AddCardCommand(lanes[0].Id, "Todo-2"), default);
        await add.Handle(new AddCardCommand(lanes[1].Id, "Doing-1"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_db);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);

        // Assert
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Todo-1");
        result[1].Title.Should().Be("Todo-2");
        result[2].Title.Should().Be("Doing-1");
    }

    [Fact]
    public async Task MoveCard_WithinLane_ReordersCorrectly()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(laneId, "A"), default);
        var b = await add.Handle(new AddCardCommand(laneId, "B"), default);
        var c = await add.Handle(new AddCardCommand(laneId, "C"), default);
        var handler = new MoveCardCommandHandler(_db);

        // Act — move C (pos 3) to position 1 → expected order: C, A, B
        await handler.Handle(new MoveCardCommand(c.Id, laneId, 1), default);

        // Assert
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
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoId = lanes[0].Id;
        var doingId = lanes[1].Id;
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(todoId, "A"), default);
        var b = await add.Handle(new AddCardCommand(todoId, "B"), default);
        await add.Handle(new AddCardCommand(doingId, "X"), default);
        var handler = new MoveCardCommandHandler(_db);

        // Act — move A from To Do to Doing at position 1 → Doing: A(1), X(2); To Do: B(1)
        await handler.Handle(new MoveCardCommand(a.Id, doingId, 1), default);

        // Assert
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
    public async Task AddCard_AssignsMonotonicallyIncreasingNumbers()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);

        // Act
        var first = await add.Handle(new AddCardCommand(laneId, "First"), default);
        var second = await add.Handle(new AddCardCommand(laneId, "Second"), default);
        var third = await add.Handle(new AddCardCommand(laneId, "Third"), default);

        // Assert
        first.Number.Should().Be(1);
        second.Number.Should().Be(2);
        third.Number.Should().Be(3);
    }

    [Fact]
    public async Task AddCard_NumberDoesNotReuseAfterDeletion()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);
        var first = await add.Handle(new AddCardCommand(laneId, "First"), default);
        await new RemoveCardCommandHandler(_db).Handle(new RemoveCardCommand(first.Id), default);

        // Act
        var second = await add.Handle(new AddCardCommand(laneId, "Second"), default);

        // Assert
        second.Number.Should().Be(2);
    }

    [Fact]
    public async Task AddCard_NumbersArePerWorkspace()
    {
        // Arrange
        var (_, lanesA) = await CreateWorkspaceWithLanesAsync();
        var nameB = U("Other");
        var workspaceB = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(nameB, $@"C:\{nameB}"), default);
        var lanesB = await new ListLanesByWorkspaceQueryHandler(_db)
            .Handle(new ListLanesByWorkspaceQuery(workspaceB.Id), default);
        var add = new AddCardCommandHandler(_db);

        // Act
        var a1 = await add.Handle(new AddCardCommand(lanesA[0].Id, "A1"), default);
        var b1 = await add.Handle(new AddCardCommand(lanesB[0].Id, "B1"), default);
        var a2 = await add.Handle(new AddCardCommand(lanesA[0].Id, "A2"), default);
        var b2 = await add.Handle(new AddCardCommand(lanesB[0].Id, "B2"), default);

        // Assert
        a1.Number.Should().Be(1);
        a2.Number.Should().Be(2);
        b1.Number.Should().Be(1);
        b2.Number.Should().Be(2);
    }

    [Fact]
    public async Task AddCard_SucceedsWhenLaneIdCasingDiffersFromStoredRow()
    {
        // Regression: older write paths stored Guid TEXT in mixed case, causing
        // Cards.LaneId -> Lanes.Id FK lookups to fail under SQLite's default
        // BINARY collation. NOCASE collation on Guid columns prevents this.

        // Arrange
        var workspaceId = Guid.NewGuid();
        var laneId = Guid.NewGuid();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Workspaces (Id, Name, Path, Position, NextCardNumber, CreatedAt, UpdatedAt) VALUES ({0}, {1}, 'C:\\ws', 1, 1, {2}, {2})",
            workspaceId.ToString().ToUpperInvariant(),
            U("WS"),
            DateTimeOffset.UtcNow);
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Lanes (Id, WorkspaceId, Name, Position) VALUES ({0}, {1}, 'To Do', 1)",
            laneId.ToString().ToLowerInvariant(),
            workspaceId.ToString().ToUpperInvariant());
        var handler = new AddCardCommandHandler(_db);

        // Act
        var act = async () => await handler.Handle(
            new AddCardCommand(laneId, "Mixed-case lane FK"), default);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MoveCard_ToEndOfLane_PlacesLast()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_db);
        var a = await add.Handle(new AddCardCommand(laneId, "A"), default);
        var b = await add.Handle(new AddCardCommand(laneId, "B"), default);
        var c = await add.Handle(new AddCardCommand(laneId, "C"), default);
        var handler = new MoveCardCommandHandler(_db);

        // Act — move A (pos 1) to position 3 (end) → expected order: B, C, A
        await handler.Handle(new MoveCardCommand(a.Id, laneId, 3), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();
        cards.Select(x => x.Title).Should().Equal("B", "C", "A");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task EditCard_UpdatesTitle()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Original"), default);
        var handler = new UpdateCardCommandHandler(_db);

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: "Renamed", Description: null, UpdateTags: false, TagNames: []),
            default);

        // Assert
        result.Title.Should().Be("Renamed");
        (await _db.Cards.FindAsync(card.Id))!.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task EditCard_UpdatesDescription()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", "Old desc"), default);
        var handler = new UpdateCardCommandHandler(_db);

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: "New desc", UpdateTags: false, TagNames: []),
            default);

        // Assert
        result.Description.Should().Be("New desc");
    }

    [Fact]
    public async Task EditCard_ReplacesTags()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", TagNames: ["bug", "urgent"]), default);
        var handler = new UpdateCardCommandHandler(_db);

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTags: true, TagNames: ["feature"]),
            default);

        // Assert
        var tagNames = await _db.CardTags
            .Where(ct => ct.CardId == card.Id)
            .Include(ct => ct.Tag)
            .Select(ct => ct.Tag.Name)
            .ToListAsync();
        tagNames.Should().BeEquivalentTo(["feature"]);
    }

    [Fact]
    public async Task EditCard_ClearsTags()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", TagNames: ["bug"]), default);
        var handler = new UpdateCardCommandHandler(_db);

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTags: true, TagNames: []),
            default);

        // Assert
        var tagCount = await _db.CardTags.CountAsync(ct => ct.CardId == card.Id);
        tagCount.Should().Be(0);
    }

    [Fact]
    public async Task EditCard_NoFieldsSupplied_Throws()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new UpdateCardCommandHandler(_db);

        // Act
        var act = async () => await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTags: false, TagNames: []),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*At least one field*");
    }

    [Fact]
    public async Task GetCard_ReflectsOutOfBandRename()
    {
        // Regression for card #6: same staleness fix as card #5, applied to GetCard.

        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Original title"), default);
        var handler = new GetCardQueryHandler(_db);
        var initial = await handler.Handle(new GetCardQuery(card.Id), default);
        initial!.Title.Should().Be("Original title");

        // Act — simulate CLI rename via a second context on the same connection
        var cliOptions = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using (var cliDb = new BishopDbContext(cliOptions))
        {
            var cliCard = await cliDb.Cards.SingleAsync(c => c.Id == card.Id);
            cliCard.Title = "Renamed title";
            await cliDb.SaveChangesAsync();
        }

        // Assert
        var refreshed = await handler.Handle(new GetCardQuery(card.Id), default);
        refreshed!.Title.Should().Be("Renamed title");
    }

    [Fact]
    public async Task ListCardsByWorkspace_ReflectsOutOfBandLaneMove()
    {
        // Regression for card #5: the UI's long-lived DbContext was caching tracked
        // entities, so a card moved via the CLI stayed in its old lane on refresh.

        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lanes[0].Id, "Move me"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_db);
        var initial = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);
        initial.Single().LaneId.Should().Be(lanes[0].Id);

        // Act — simulate CLI move via a second context on the same connection
        var cliOptions = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using (var cliDb = new BishopDbContext(cliOptions))
        {
            var cliCard = await cliDb.Cards.SingleAsync(c => c.Id == card.Id);
            cliCard.LaneId = lanes[1].Id;
            await cliDb.SaveChangesAsync();
        }

        // Assert
        var refreshed = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);
        refreshed.Single().LaneId.Should().Be(lanes[1].Id);
    }
}
