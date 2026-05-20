using Bishop.App.Cards.AddCard;
using Bishop.App.Lanes.AddLane;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Lanes.MoveLane;
using Bishop.App.Lanes.RemoveLane;
using Bishop.App.Lanes.RenameLane;
using Bishop.App.Lanes.ReorderLanes;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Lanes;

public sealed class LaneHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly SqliteConnection _connection;

    public LaneHandlerTests(DbFixture fixture)
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
    public async Task CreateWorkspace_SeedsThreeLanes()
    {
        // Arrange
        var name = U("Seeded");
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new ListLanesByWorkspaceQueryHandler(_db);

        // Act
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);

        // Assert
        lanes.Should().HaveCount(3);
        lanes.Select(l => l.Name).Should().Equal("To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ListLanesByWorkspace_ReturnsOnlyLanesForThatWorkspace()
    {
        // Arrange
        var n1 = U("WS1");
        var n2 = U("WS2");
        var ws1 = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(n1, $@"C:\{n1}"), default);
        var ws2 = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(n2, $@"C:\{n2}"), default);
        var handler = new ListLanesByWorkspaceQueryHandler(_db);

        // Act
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(ws1.Id), default);

        // Assert
        lanes.Should().HaveCount(3);
        lanes.Should().AllSatisfy(l => l.WorkspaceId.Should().Be(ws1.Id));
    }

    [Fact]
    public async Task AddLane_PersistsAndReturnsLane()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new AddLaneCommandHandler(_db);

        // Act
        var result = await handler.Handle(new AddLaneCommand(workspace.Id, "Backlog"), default);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Backlog");
        result.WorkspaceId.Should().Be(workspace.Id);
        result.Position.Should().Be(4);
    }

    [Fact]
    public async Task AddLane_AssignsSequentialPositions()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new AddLaneCommandHandler(_db);

        // Act
        var fourth = await handler.Handle(new AddLaneCommand(workspace.Id, "Review"), default);
        var fifth = await handler.Handle(new AddLaneCommand(workspace.Id, "Deploy"), default);

        // Assert
        fourth.Position.Should().Be(4);
        fifth.Position.Should().Be(5);
    }

    [Fact]
    public async Task RenameLane_UpdatesName()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];
        var handler = new RenameLaneCommandHandler(_db);

        // Act
        var result = await handler.Handle(new RenameLaneCommand(todo.Id, "Inbox"), default);

        // Assert
        result.Id.Should().Be(todo.Id);
        result.Name.Should().Be("Inbox");
        var persisted = await _db.Lanes.FindAsync(todo.Id);
        persisted!.Name.Should().Be("Inbox");
    }

    [Fact]
    public async Task MoveLane_ReordersLanesCorrectly()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var done = lanes[2];
        var handler = new MoveLaneCommandHandler(_db);

        // Act — move "Done" (pos 3) to position 1 → Done, To Do, Doing
        await handler.Handle(new MoveLaneCommand(done.Id, 1), default);

        // Assert
        var updated = await _db.Lanes
            .Where(l => l.WorkspaceId == done.WorkspaceId)
            .OrderBy(l => l.Position)
            .ToListAsync();
        updated.Select(l => l.Name).Should().Equal("Done", "To Do", "Doing");
        updated.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MoveLane_ToEnd_PlacesLast()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];
        var handler = new MoveLaneCommandHandler(_db);

        // Act — move "To Do" (pos 1) to position 3 → Doing, Done, To Do
        await handler.Handle(new MoveLaneCommand(todo.Id, 3), default);

        // Assert
        var updated = await _db.Lanes
            .Where(l => l.WorkspaceId == todo.WorkspaceId)
            .OrderBy(l => l.Position)
            .ToListAsync();
        updated.Select(l => l.Name).Should().Equal("Doing", "Done", "To Do");
        updated.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task RemoveLane_DeletesLane_AndRenumbersRemaining()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var doing = lanes[1];
        var handler = new RemoveLaneCommandHandler(_db);

        // Act
        await handler.Handle(new RemoveLaneCommand(doing.Id), default);

        // Assert
        var remaining = await _db.Lanes
            .Where(l => l.WorkspaceId == doing.WorkspaceId)
            .OrderBy(l => l.Position)
            .ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Select(l => l.Name).Should().Equal("To Do", "Done");
        remaining.Select(l => l.Position).Should().Equal(1, 2);
    }

    [Fact]
    public async Task RemoveLane_ThrowsWhenNonEmpty()
    {
        // Arrange
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];
        await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(todo.Id, "A task"), default);
        var handler = new RemoveLaneCommandHandler(_db);

        // Act
        var act = async () => await handler.Handle(new RemoveLaneCommand(todo.Id), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not empty*");
    }

    [Fact]
    public async Task ReorderLanes_PersistsNewPositions()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];  // pos 1
        var doing = lanes[1]; // pos 2
        var done = lanes[2];  // pos 3
        var handler = new ReorderLanesCommandHandler(_db);

        // Act — reverse order: Done, To Do, Doing
        await handler.Handle(
            new ReorderLanesCommand(workspace.Id, [done.Id, todo.Id, doing.Id]),
            default);

        // Assert
        var updated = await _db.Lanes
            .Where(l => l.WorkspaceId == workspace.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        updated.Select(l => l.Name).Should().Equal("Done", "To Do", "Doing");
        updated.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReorderLanes_PartialOrderedIds_LeavesUnmappedLanesUnchanged()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];  // pos 1
        var doing = lanes[1]; // pos 2
        var done = lanes[2];  // pos 3
        var handler = new ReorderLanesCommandHandler(_db);

        // Act — supply only Done and To Do; Doing is omitted from the list
        await handler.Handle(
            new ReorderLanesCommand(workspace.Id, [done.Id, todo.Id]),
            default);

        // Assert — mapped lanes get new positions; Doing retains its original position
        var doneLane = await _db.Lanes.FindAsync(done.Id);
        var todoLane = await _db.Lanes.FindAsync(todo.Id);
        var doingLane = await _db.Lanes.FindAsync(doing.Id);
        doneLane!.Position.Should().Be(1);
        todoLane!.Position.Should().Be(2);
        doingLane!.Position.Should().Be(2); // unchanged
    }

    [Fact]
    public async Task ReorderLanes_DoesNotAffectOtherWorkspace()
    {
        // Arrange
        var (ws1, lanes1) = await CreateWorkspaceWithLanesAsync();
        var (ws2, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ReorderLanesCommandHandler(_db);

        // Act — reorder ws1 lanes only
        await handler.Handle(
            new ReorderLanesCommand(ws1.Id, [lanes1[2].Id, lanes1[1].Id, lanes1[0].Id]),
            default);

        // Assert — ws2 lanes are unchanged
        var ws2Lanes = await _db.Lanes
            .Where(l => l.WorkspaceId == ws2.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        ws2Lanes.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ListLanesByWorkspace_ReflectsOutOfBandRename()
    {
        // Regression for card #5: a lane renamed via the CLI must surface on refresh
        // in the UI (the UI's long-lived DbContext was caching tracked entities).

        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var handler = new ListLanesByWorkspaceQueryHandler(_db);
        var initial = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        initial[0].Name.Should().Be("To Do");

        // Act — simulate CLI rename via a second context on the same connection
        var cliOptions = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using (var cliDb = new BishopDbContext(cliOptions))
        {
            var cliLane = await cliDb.Lanes.SingleAsync(l => l.Id == lanes[0].Id);
            cliLane.Name = "Renamed";
            await cliDb.SaveChangesAsync();
        }

        // Assert
        var refreshed = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        refreshed[0].Name.Should().Be("Renamed");
    }
}
