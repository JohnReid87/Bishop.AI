using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.GitHub;
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

public sealed class CardHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public CardHandlerTests(DbFixture fixture)
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

    [Fact]
    public async Task AddCard_PersistsAndReturnsCard()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoLane = lanes[0];
        var handler = new AddCardCommandHandler(_factory);

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
    public async Task AddCard_InsertsAtTopOfLane()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_factory);

        // Act
        await add.Handle(new AddCardCommand(laneId, "First"), default);
        var second = await add.Handle(new AddCardCommand(laneId, "Second"), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();
        second.Position.Should().Be(1);
        cards.Select(x => x.Title).Should().Equal("Second", "First");
        cards.Select(x => x.Position).Should().Equal(1, 2);
    }

    [Fact]
    public async Task RemoveCard_DeletesCard()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "ToRemove"), default);

        // Act
        await new RemoveCardCommandHandler(_factory)
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
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(lanes[0].Id, "Todo-1"), default);
        await add.Handle(new AddCardCommand(lanes[0].Id, "Todo-2"), default);
        await add.Handle(new AddCardCommand(lanes[1].Id, "Doing-1"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);

        // Assert
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Todo-2");
        result[1].Title.Should().Be("Todo-1");
        result[2].Title.Should().Be("Doing-1");
    }

    [Fact]
    public async Task MoveCard_WithinLane_ReordersCorrectly()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(laneId, "A"), default);
        var b = await add.Handle(new AddCardCommand(laneId, "B"), default);
        var c = await add.Handle(new AddCardCommand(laneId, "C"), default);
        var handler = new MoveCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act — with insert-at-top, initial order is C(1), B(2), A(3); move A to position 1 → A, C, B
        await handler.Handle(new MoveCardCommand(a.Id, laneId, 1), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();
        cards.Select(x => x.Title).Should().Equal("A", "C", "B");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveCard_AcrossLanes_ReordersSourceAndTarget()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoId = lanes[0].Id;
        var doingId = lanes[1].Id;
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(todoId, "A"), default);
        var b = await add.Handle(new AddCardCommand(todoId, "B"), default);
        await add.Handle(new AddCardCommand(doingId, "X"), default);
        var handler = new MoveCardCommandHandler(_factory, Substitute.For<ISender>());

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
        var add = new AddCardCommandHandler(_factory);

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
        var add = new AddCardCommandHandler(_factory);
        var first = await add.Handle(new AddCardCommand(laneId, "First"), default);
        await new RemoveCardCommandHandler(_factory).Handle(new RemoveCardCommand(first.Id), default);

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
        var workspaceB = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(nameB, $@"C:\{nameB}"), default);
        var lanesB = await new ListLanesByWorkspaceQueryHandler(_factory)
            .Handle(new ListLanesByWorkspaceQuery(workspaceB.Id), default);
        var add = new AddCardCommandHandler(_factory);

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
        var handler = new AddCardCommandHandler(_factory);

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
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(laneId, "A"), default);
        var b = await add.Handle(new AddCardCommand(laneId, "B"), default);
        var c = await add.Handle(new AddCardCommand(laneId, "C"), default);
        var handler = new MoveCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act — with insert-at-top, initial order is C(1), B(2), A(3); move C to position 3 (end) → B, A, C
        await handler.Handle(new MoveCardCommand(c.Id, laneId, 3), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();
        cards.Select(x => x.Title).Should().Equal("B", "A", "C");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveCard_WithExpectedSourceLaneId_MatchingCurrentLane_Succeeds()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoId = lanes.Single(l => l.Name == SystemLaneNames.ToDo).Id;
        var doingId = lanes.Single(l => l.Name == SystemLaneNames.Doing).Id;
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(todoId, "A"), default);
        var handler = new MoveCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new MoveCardCommand(card.Id, doingId, 1, ExpectedSourceLaneId: todoId),
            default);

        // Assert
        var moved = await _db.Cards.FindAsync(card.Id);
        moved!.LaneId.Should().Be(doingId);
    }

    [Fact]
    public async Task MoveCard_WithExpectedSourceLaneId_MismatchedLane_Throws()
    {
        // Arrange — simulate the optimistic-concurrency case: caller believed the card
        // was still in To Do, but it has already been moved to Doing by another writer.
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoId = lanes.Single(l => l.Name == SystemLaneNames.ToDo).Id;
        var doingId = lanes.Single(l => l.Name == SystemLaneNames.Doing).Id;
        var doneId = lanes.Single(l => l.Name == SystemLaneNames.Done).Id;
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(doingId, "Already moved"), default);
        var handler = new MoveCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        var act = async () => await handler.Handle(
            new MoveCardCommand(card.Id, doneId, 1, ExpectedSourceLaneId: todoId),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Card {card.Id} was expected in lane {todoId} but is now in lane {doingId}.");
        var unchanged = await _db.Cards.FindAsync(card.Id);
        unchanged!.LaneId.Should().Be(doingId, "the handler must not mutate when the guard fails");
    }

    [Fact]
    public async Task EditCard_UpdatesTitle()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Original"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: "Renamed", Description: null, UpdateTag: false, TagName: null),
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
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", "Old desc"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: "New desc", UpdateTag: false, TagName: null),
            default);

        // Assert
        result.Description.Should().Be("New desc");
    }

    [Fact]
    public async Task EditCard_ReplacesTag()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", TagName: "bug"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: true, TagName: "feature"),
            default);

        // Assert
        var updated = await _db.Cards.FindAsync(card.Id);
        var tag = await _db.Tags.FindAsync(updated!.TagId);
        tag!.Name.Should().Be("feature");
    }

    [Fact]
    public async Task EditCard_ClearsTag()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", TagName: "bug"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: true, TagName: null),
            default);

        // Assert
        var updated = await _db.Cards.FindAsync(card.Id);
        updated!.TagId.Should().BeNull();
    }

    [Fact]
    public async Task EditCard_NoFieldsSupplied_Throws()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        var act = async () => await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*At least one field*");
    }

    [Fact]
    public async Task EditCard_AppendDescription_ToExistingDescription_AppendsWithSeparator()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task", "Existing"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                AppendDescription: "Appended"),
            default);

        // Assert
        result.Description.Should().Be("Existing\n\n---\n\nAppended");
        (await _db.Cards.FindAsync(card.Id))!.Description.Should().Be("Existing\n\n---\n\nAppended");
    }

    [Fact]
    public async Task EditCard_AppendDescription_ToEmptyDescription_SetsDescriptionWithNoSeparator()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                AppendDescription: "First content"),
            default);

        // Assert
        result.Description.Should().Be("First content");
        (await _db.Cards.FindAsync(card.Id))!.Description.Should().Be("First content");
    }

    [Fact]
    public async Task EditCard_AppendDescriptionWithToLane_MovesCardAndAutoCloses()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var doneLane = lanes.Single(l => l.Name == "Done");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes.Single(l => l.Name == "To Do").Id, "Task", "Existing"), default);
        var handler = new UpdateCardCommandHandler(_factory, CreateSender());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                AppendDescription: "### Agent notes\nDone.", ToLaneId: doneLane.Id),
            default);

        // Assert — description appended, card moved to Done and auto-closed
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.Description.Should().Be("Existing\n\n---\n\n### Agent notes\nDone.");
        persisted.LaneId.Should().Be(doneLane.Id);
        persisted.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task EditCard_ToLaneWithKeepOpen_MovesCardWithoutClosing()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var doneLane = lanes.Single(l => l.Name == "Done");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes.Single(l => l.Name == "To Do").Id, "Task", "Existing"), default);
        var handler = new UpdateCardCommandHandler(_factory, CreateSender());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                AppendDescription: "### Agent notes\nDone.", ToLaneId: doneLane.Id, KeepOpen: true),
            default);

        // Assert — card moved to Done but IsClosed remains false
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.LaneId.Should().Be(doneLane.Id);
        persisted.IsClosed.Should().BeFalse();
    }

    private ISender CreateSender()
    {
        var ghCli = Substitute.For<IGhCli>();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_factory, sender)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<CloseCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new CloseCardCommandHandler(_factory, ghCli)
                .Handle(call.ArgAt<CloseCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    [Fact]
    public async Task GetCard_ReflectsOutOfBandRename()
    {
        // Regression for card #6: same staleness fix as card #5, applied to GetCard.

        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Original title"), default);
        var handler = new GetCardQueryHandler(_factory);
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

    // ── GetCardByNumber ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetCardByNumber_ReturnsCard_WhenMatchFound()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var added = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Find me"), default);
        var handler = new GetCardByNumberQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetCardByNumberQuery(added.Number, workspace.Id), default);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(added.Id);
        result.Title.Should().Be("Find me");
        result.Lane.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCardByNumber_ReturnsNull_WhenNumberNotFound()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new GetCardByNumberQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetCardByNumberQuery(9999, workspace.Id), default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCardByNumber_ReturnsNull_WhenWrongWorkspace()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var added = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Task"), default);
        var handler = new GetCardByNumberQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetCardByNumberQuery(added.Number, Guid.NewGuid()), default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListCardsByWorkspace_ReflectsOutOfBandLaneMove()
    {
        // Regression for card #5: the UI's long-lived DbContext was caching tracked
        // entities, so a card moved via the CLI stayed in its old lane on refresh.

        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(lanes[0].Id, "Move me"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);
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

    [Fact]
    public async Task AddCard_Bottom_InsertsAfterLastCard()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(laneId, "First"), default);
        await add.Handle(new AddCardCommand(laneId, "Second"), default);

        // Act
        var bottom = await add.Handle(new AddCardCommand(laneId, "Third", Position: CardInsertPosition.Bottom), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.LaneId == laneId)
            .OrderBy(x => x.Position)
            .ToListAsync();
        bottom.Position.Should().Be(3);
        cards.Select(x => x.Title).Should().Equal("Second", "First", "Third");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task AddCard_Bottom_DoesNotShiftExistingCards()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_factory);
        var existing = await add.Handle(new AddCardCommand(laneId, "Existing"), default);

        // Act
        await add.Handle(new AddCardCommand(laneId, "Bottom", Position: CardInsertPosition.Bottom), default);

        // Assert
        var refreshed = await _db.Cards.FindAsync(existing.Id);
        refreshed!.Position.Should().Be(existing.Position);
    }

    [Fact]
    public async Task AddCard_Bottom_IntoEmptyLane_PlacesAtPositionOne()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneId = lanes[0].Id;
        var add = new AddCardCommandHandler(_factory);

        // Act
        var card = await add.Handle(new AddCardCommand(laneId, "Only", Position: CardInsertPosition.Bottom), default);

        // Assert
        card.Position.Should().Be(1);
    }
}
