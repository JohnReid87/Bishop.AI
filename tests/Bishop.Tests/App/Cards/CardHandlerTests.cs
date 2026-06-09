using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    private MoveCardCommandHandler MoveHandler() =>
        new(_factory);

    private async Task<(Workspace workspace, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceWithLanesAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    [Fact]
    public async Task AddCard_PersistsAndReturnsCard()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todoLane = lanes[0];
        var handler = new AddCardCommandHandler(_factory);

        // Act
        var result = await handler.Handle(new AddCardCommand(workspace.Id, todoLane.Name, "My Task", "Some details"), default);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("My Task");
        result.Description.Should().Be("Some details");
        result.LaneName.Should().Be(todoLane.Name);
        result.WorkspaceId.Should().Be(workspace.Id);
        result.Position.Should().Be(1);
        result.CreatedAt.Should().NotBe(default);
        result.UpdatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task AddCard_InsertsAtTopOfLane()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var lane = lanes[0];
        var add = new AddCardCommandHandler(_factory);

        // Act
        await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "First"), default);
        var second = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "Second"), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == lane.Name)
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "ToRemove"), default);

        // Act
        await new RemoveCardCommandHandler(_factory)
            .Handle(new RemoveCardCommand(card.Id), default);

        // Assert
        var remaining = await _db.Cards.FindAsync(card.Id);
        remaining.Should().BeNull();
    }

    [Fact]
    public async Task RemoveCard_NonexistentCard_Throws()
    {
        var handler = new RemoveCardCommandHandler(_factory);

        var act = () => handler.Handle(new RemoveCardCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task ListCardsByWorkspace_ReturnsCardsOrderedByLaneThenPosition()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Todo-1"), default);
        await add.Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Todo-2"), default);
        await add.Handle(new AddCardCommand(workspace.Id, lanes[1].Name, "Doing-1"), default);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var lane = lanes[0];
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "B"), default);
        var c = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "C"), default);
        var handler = MoveHandler();

        // Act — with insert-at-top, initial order is C(1), B(2), A(3); move A to position 1 → A, C, B
        await handler.Handle(new MoveCardCommand(a.Id, lane.Name, 1), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == lane.Name)
            .OrderBy(x => x.Position)
            .ToListAsync();
        cards.Select(x => x.Title).Should().Equal("A", "C", "B");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveCard_AcrossLanes_ReordersSourceAndTarget()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];
        var doing = lanes[1];
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "B"), default);
        await add.Handle(new AddCardCommand(workspace.Id, doing.Name, "X"), default);
        var handler = MoveHandler();

        // Act — move A from To Do to Doing at position 1 → Doing: A(1), X(2); To Do: B(1)
        await handler.Handle(new MoveCardCommand(a.Id, doing.Name, 1), default);

        // Assert
        var todoCards = await _db.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == todo.Name)
            .OrderBy(x => x.Position)
            .ToListAsync();
        var doingCards = await _db.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == doing.Name)
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneName = lanes[0].Name;
        var add = new AddCardCommandHandler(_factory);

        // Act
        var first = await add.Handle(new AddCardCommand(workspace.Id, laneName, "First"), default);
        var second = await add.Handle(new AddCardCommand(workspace.Id, laneName, "Second"), default);
        var third = await add.Handle(new AddCardCommand(workspace.Id, laneName, "Third"), default);

        // Assert
        first.Number.Should().Be(1);
        second.Number.Should().Be(2);
        third.Number.Should().Be(3);
    }

    [Fact]
    public async Task AddCard_NumberDoesNotReuseAfterDeletion()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneName = lanes[0].Name;
        var add = new AddCardCommandHandler(_factory);
        var first = await add.Handle(new AddCardCommand(workspace.Id, laneName, "First"), default);
        await new RemoveCardCommandHandler(_factory).Handle(new RemoveCardCommand(first.Id), default);

        // Act
        var second = await add.Handle(new AddCardCommand(workspace.Id, laneName, "Second"), default);

        // Assert
        second.Number.Should().Be(2);
    }

    [Fact]
    public async Task AddCard_NumbersArePerWorkspace()
    {
        // Arrange
        var (workspaceA, lanesA) = await CreateWorkspaceWithLanesAsync();
        var nameB = U("Other");
        var workspaceB = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(nameB, $@"C:\{nameB}"), default);
        var lanesB = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspaceB.Id), default);
        var add = new AddCardCommandHandler(_factory);

        // Act
        var a1 = await add.Handle(new AddCardCommand(workspaceA.Id, lanesA[0].Name, "A1"), default);
        var b1 = await add.Handle(new AddCardCommand(workspaceB.Id, lanesB[0].Name, "B1"), default);
        var a2 = await add.Handle(new AddCardCommand(workspaceA.Id, lanesA[0].Name, "A2"), default);
        var b2 = await add.Handle(new AddCardCommand(workspaceB.Id, lanesB[0].Name, "B2"), default);

        // Assert
        a1.Number.Should().Be(1);
        a2.Number.Should().Be(2);
        b1.Number.Should().Be(1);
        b2.Number.Should().Be(2);
    }

    [Fact]
    public async Task AddCard_SucceedsWhenWorkspaceIdCasingDiffersFromStoredRow()
    {
        // Regression: older write paths stored Guid TEXT in mixed case, causing
        // cross-table Guid FK lookups (e.g. Cards.WorkspaceId -> Workspaces.Id)
        // to fail under SQLite's default BINARY collation. NOCASE collation on
        // Guid columns prevents this.

        // Arrange
        var workspaceId = Guid.NewGuid();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Workspaces (Id, Name, Path, Position, NextCardNumber, IsRemoved, CreatedAt, UpdatedAt) VALUES ({0}, {1}, 'C:\\ws', 1, 1, 0, {2}, {2})",
            workspaceId.ToString().ToUpperInvariant(),
            U("WS"),
            DateTimeOffset.UtcNow);
        var handler = new AddCardCommandHandler(_factory);

        // Act
        var act = async () => await handler.Handle(
            new AddCardCommand(workspaceId, SystemLaneNames.ToDo, "Mixed-case workspace FK"), default);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MoveCard_ToEndOfLane_PlacesLast()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var lane = lanes[0];
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "B"), default);
        var c = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "C"), default);
        var handler = MoveHandler();

        // Act — with insert-at-top, initial order is C(1), B(2), A(3); move C to position 3 (end) → B, A, C
        await handler.Handle(new MoveCardCommand(c.Id, lane.Name, 3), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.LaneName == lane.Name)
            .OrderBy(x => x.Position)
            .ToListAsync();
        cards.Select(x => x.Title).Should().Equal("B", "A", "C");
        cards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveCard_WithExpectedSourceLaneName_MatchingCurrentLane_Succeeds()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "A"), default);
        var handler = MoveHandler();

        // Act
        await handler.Handle(
            new MoveCardCommand(card.Id, SystemLaneNames.Doing, 1, ExpectedSourceLaneName: SystemLaneNames.ToDo),
            default);

        // Assert
        var moved = await _db.Cards.FindAsync(card.Id);
        moved!.LaneName.Should().Be(SystemLaneNames.Doing);
    }

    [Fact]
    public async Task MoveCard_WithExpectedSourceLaneName_MismatchedLane_Throws()
    {
        // Arrange — simulate the optimistic-concurrency case: caller believed the card
        // was still in To Do, but it has already been moved to Doing by another writer.
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.Doing, "Already moved"), default);
        var handler = MoveHandler();

        // Act
        var act = async () => await handler.Handle(
            new MoveCardCommand(card.Id, SystemLaneNames.Done, 1, ExpectedSourceLaneName: SystemLaneNames.ToDo),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Card {card.Id} was expected in lane '{SystemLaneNames.ToDo}' but is in lane '{SystemLaneNames.Doing}'.");
        var unchanged = await _db.Cards.FindAsync(card.Id);
        unchanged!.LaneName.Should().Be(SystemLaneNames.Doing, "the handler must not mutate when the guard fails");
    }

    [Fact]
    public async Task MoveCard_NonexistentCard_Throws()
    {
        var handler = MoveHandler();

        var act = () => handler.Handle(new MoveCardCommand(Guid.NewGuid(), SystemLaneNames.ToDo, 1), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task MoveCard_AcrossLanes_SourceLaneRenumbersAscendingWithMultipleRemainingCards()
    {
        // Arrange — top-inserts produce D(1) C(2) B(3) A(4) in To Do
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "A"), default);
        await add.Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "B"), default);
        await add.Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "C"), default);
        var d = await add.Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "D"), default);

        // Act — move D (position 1) to Doing; remaining C(2) B(3) A(4) must renumber to 1 2 3
        await MoveHandler().Handle(new MoveCardCommand(d.Id, SystemLaneNames.Doing, 1), default);

        // Assert — remaining source-lane cards are renumbered 1-based ascending by prior position
        var sourceCards = await _db.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == SystemLaneNames.ToDo)
            .OrderBy(x => x.Position)
            .ToListAsync();
        sourceCards.Select(x => x.Title).Should().Equal("C", "B", "A");
        sourceCards.Select(x => x.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveCard_AcrossLanes_SourceLaneRenumberingIsIsolatedByWorkspace()
    {
        // Arrange — two workspaces with cards in the same lane name;
        // renumbering after a move in workspace A must not touch workspace B's cards.
        var (wsA, _) = await CreateWorkspaceWithLanesAsync();
        var (wsB, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);

        await add.Handle(new AddCardCommand(wsA.Id, SystemLaneNames.ToDo, "A1"), default);
        var a2 = await add.Handle(new AddCardCommand(wsA.Id, SystemLaneNames.ToDo, "A2"), default);

        var b1 = await add.Handle(new AddCardCommand(wsB.Id, SystemLaneNames.ToDo, "B1"), default);
        var b2 = await add.Handle(new AddCardCommand(wsB.Id, SystemLaneNames.ToDo, "B2"), default);

        var b1PosBefore = (await _db.Cards.FindAsync(b1.Id))!.Position;
        var b2PosBefore = (await _db.Cards.FindAsync(b2.Id))!.Position;

        // Act — move a2 (position 1 in wsA's To Do) to wsA's Doing
        await MoveHandler().Handle(new MoveCardCommand(a2.Id, SystemLaneNames.Doing, 1), default);

        // Assert — workspace B's card positions are unchanged
        _db.ChangeTracker.Clear();
        (await _db.Cards.FindAsync(b1.Id))!.Position.Should().Be(b1PosBefore);
        (await _db.Cards.FindAsync(b2.Id))!.Position.Should().Be(b2PosBefore);
    }

    [Fact]
    public async Task EditCard_UpdatesTitle()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Original"), default);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task", "Old desc"), default);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task", TagName: "bug"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: true, TagName: "feature"),
            default);

        // Assert
        var updated = await _db.Cards.FindAsync(card.Id);
        updated!.TagName.Should().Be("feature");
    }

    [Fact]
    public async Task EditCard_ClearsTag()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task", TagName: "bug"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: true, TagName: null),
            default);

        // Assert
        var updated = await _db.Cards.FindAsync(card.Id);
        updated!.TagName.Should().BeNull();
    }

    [Fact]
    public async Task EditCard_NoFieldsSupplied_Throws()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task", "Existing"), default);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var doneLane = lanes.Single(l => l.Name == "Done");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Task", "Existing"), default);
        var handler = new UpdateCardCommandHandler(_factory, CreateSender());

        // Act
        var result = await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                AppendDescription: "### Agent notes\nDone.", ToLaneName: doneLane.Name),
            default);

        // Assert — description appended, card moved to Done and auto-closed
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.Description.Should().Be("Existing\n\n---\n\n### Agent notes\nDone.");
        persisted.LaneName.Should().Be(doneLane.Name);
        persisted.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task EditCard_CommitHashOnly_PersistsHash()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                CommitHash: "abc123"),
            default);

        // Assert
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.CommitHash.Should().Be("abc123");
        persisted.BranchName.Should().BeNull();
    }

    [Fact]
    public async Task EditCard_CommitHashAndBranch_PersistsBoth()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                CommitHash: "deadbeef", CommitBranchName: "main"),
            default);

        // Assert
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.CommitHash.Should().Be("deadbeef");
        persisted.BranchName.Should().Be("main");
    }

    [Fact]
    public async Task EditCard_CommitFieldsOmitted_LeavesExistingCommitMetadataUntouched()
    {
        // Arrange — seed the card with prior commit metadata, then edit an unrelated field
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
        await using (var seedDb = await _factory.CreateDbContextAsync())
        {
            var seedCard = await seedDb.Cards.FindAsync(card.Id);
            seedCard!.CommitHash = "preexisting";
            seedCard.BranchName = "feature/x";
            await seedDb.SaveChangesAsync();
        }
        _db.ChangeTracker.Clear();

        var handler = new UpdateCardCommandHandler(_factory, Substitute.For<ISender>());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: "Renamed", Description: null, UpdateTag: false, TagName: null),
            default);

        // Assert
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.CommitHash.Should().Be("preexisting");
        persisted.BranchName.Should().Be("feature/x");
        persisted.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task EditCard_CommitHashWithAppendAndToLane_PersistsAllInOneCall()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var doneLane = lanes.Single(l => l.Name == "Done");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Task", "Existing"), default);
        var handler = new UpdateCardCommandHandler(_factory, CreateSender());

        // Act
        await handler.Handle(
            new UpdateCardCommand(card.Id, Title: null, Description: null, UpdateTag: false, TagName: null,
                AppendDescription: "### Agent notes\nDone.", ToLaneName: doneLane.Name,
                CommitHash: "cafebabe", CommitBranchName: "main"),
            default);

        // Assert
        var persisted = await _db.Cards.FindAsync(card.Id);
        persisted!.Description.Should().Be("Existing\n\n---\n\n### Agent notes\nDone.");
        persisted.LaneName.Should().Be(doneLane.Name);
        persisted.IsClosed.Should().BeTrue();
        persisted.CommitHash.Should().Be("cafebabe");
        persisted.BranchName.Should().Be("main");
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<MoveCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => new MoveCardCommandHandler(_factory)
                .Handle(call.ArgAt<MoveCardCommand>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    [Fact]
    public async Task GetCard_ReflectsOutOfBandRename()
    {
        // Regression for card #6: same staleness fix as card #5, applied to GetCard.

        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Original title"), default);
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
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Find me"), default);
        var handler = new GetCardByNumberQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new GetCardByNumberQuery(added.Number, workspace.Id), default);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(added.Id);
        result.Title.Should().Be("Find me");
        result.LaneName.Should().Be(lanes[0].Name);
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var added = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Task"), default);
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
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Move me"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);
        var initial = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);
        initial.Single().LaneName.Should().Be(lanes[0].Name);

        // Act — simulate CLI move via a second context on the same connection
        var cliOptions = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using (var cliDb = new BishopDbContext(cliOptions))
        {
            var cliCard = await cliDb.Cards.SingleAsync(c => c.Id == card.Id);
            cliCard.LaneName = lanes[1].Name;
            await cliDb.SaveChangesAsync();
        }

        // Assert
        var refreshed = await handler.Handle(new ListCardsByWorkspaceQuery(workspace.Id), default);
        refreshed.Single().LaneName.Should().Be(lanes[1].Name);
    }

    [Fact]
    public async Task AddCard_Bottom_InsertsAfterLastCard()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var lane = lanes[0];
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "First"), default);
        await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "Second"), default);

        // Act
        var bottom = await add.Handle(new AddCardCommand(workspace.Id, lane.Name, "Third", Position: CardInsertPosition.Bottom), default);

        // Assert
        var cards = await _db.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == lane.Name)
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
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneName = lanes[0].Name;
        var add = new AddCardCommandHandler(_factory);
        var existing = await add.Handle(new AddCardCommand(workspace.Id, laneName, "Existing"), default);

        // Act
        await add.Handle(new AddCardCommand(workspace.Id, laneName, "Bottom", Position: CardInsertPosition.Bottom), default);

        // Assert
        var refreshed = await _db.Cards.FindAsync(existing.Id);
        refreshed!.Position.Should().Be(existing.Position);
    }

    [Fact]
    public async Task AddCard_Bottom_IntoEmptyLane_PlacesAtPositionOne()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var laneName = lanes[0].Name;
        var add = new AddCardCommandHandler(_factory);

        // Act
        var card = await add.Handle(new AddCardCommand(workspace.Id, laneName, "Only", Position: CardInsertPosition.Bottom), default);

        // Assert
        card.Position.Should().Be(1);
    }

    [Fact]
    public async Task AddCard_InvalidWorkspaceId_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new AddCardCommandHandler(_factory);

        // Act
        var act = () => handler.Handle(new AddCardCommand(Guid.NewGuid(), SystemLaneNames.ToDo, "X"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task AddCard_Top_PositionsAreIsolatedByWorkspace()
    {
        // Arrange — two workspaces share the same lane name; a top-insert into workspace B
        // must not shift the positions of workspace A's cards in that lane.
        var (wsA, _) = await CreateWorkspaceWithLanesAsync();
        var (wsB, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        const string lane = SystemLaneNames.ToDo;

        // After three top-inserts into workspace A, positions are A3=1, A2=2, A1=3
        await add.Handle(new AddCardCommand(wsA.Id, lane, "A1"), default);
        await add.Handle(new AddCardCommand(wsA.Id, lane, "A2"), default);
        await add.Handle(new AddCardCommand(wsA.Id, lane, "A3"), default);

        // Act
        var bCard = await add.Handle(new AddCardCommand(wsB.Id, lane, "B1"), default);

        // Assert — workspace A's positions are unchanged (1, 2, 3)
        await using var verify = _factory.CreateDbContext();
        var wsAPositions = await verify.Cards
            .Where(c => c.WorkspaceId == wsA.Id && c.LaneName == lane)
            .OrderBy(c => c.Position)
            .Select(c => c.Position)
            .ToListAsync();

        bCard.Position.Should().Be(1, "workspace B's lane is empty so the new card lands at position 1");
        wsAPositions.Should().Equal(new[] { 1, 2, 3 },
            "adding a card to workspace B must not displace workspace A's card positions");
    }

    [Fact]
    public async Task AddCard_Bottom_PositionIsIsolatedByWorkspace()
    {
        // Arrange — workspace A has cards (MaxPosition = 3 in its ToDo lane); workspace B is empty.
        // A bottom-insert into workspace B must use only workspace B's cards when computing MaxPosition.
        var (wsA, _) = await CreateWorkspaceWithLanesAsync();
        var (wsB, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        const string lane = SystemLaneNames.ToDo;

        await add.Handle(new AddCardCommand(wsA.Id, lane, "A1"), default);
        await add.Handle(new AddCardCommand(wsA.Id, lane, "A2"), default);
        await add.Handle(new AddCardCommand(wsA.Id, lane, "A3"), default);

        // Act
        var bCard = await add.Handle(
            new AddCardCommand(wsB.Id, lane, "B1", Position: CardInsertPosition.Bottom), default);

        // Assert — (null ?? 0) + 1 = 1 because workspace B has no cards
        bCard.Position.Should().Be(1,
            "workspace B's ToDo lane is empty; bottom of an empty lane is position 1");
    }
}
