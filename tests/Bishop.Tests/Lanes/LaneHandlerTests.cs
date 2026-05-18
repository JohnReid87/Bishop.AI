using Bishop.App.Cards.AddCard;
using Bishop.App.Lanes.AddLane;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Lanes.MoveLane;
using Bishop.App.Lanes.RemoveLane;
using Bishop.App.Lanes.RenameLane;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Lanes;

public sealed class LaneHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BishopDbContext _db;

    public LaneHandlerTests()
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
    public async Task CreateWorkspace_SeedsThreeLanes()
    {
        var workspace = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("Seeded", @"C:\seeded"), default);

        var handler = new ListLanesByWorkspaceQueryHandler(_db);
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);

        lanes.Should().HaveCount(3);
        lanes.Select(l => l.Name).Should().Equal("To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ListLanesByWorkspace_ReturnsOnlyLanesForThatWorkspace()
    {
        var ws1 = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("WS1", @"C:\ws1"), default);
        var ws2 = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand("WS2", @"C:\ws2"), default);

        var handler = new ListLanesByWorkspaceQueryHandler(_db);
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(ws1.Id), default);

        lanes.Should().HaveCount(3);
        lanes.Should().AllSatisfy(l => l.WorkspaceId.Should().Be(ws1.Id));
    }

    [Fact]
    public async Task AddLane_PersistsAndReturnsLane()
    {
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();

        var handler = new AddLaneCommandHandler(_db);
        var result = await handler.Handle(new AddLaneCommand(workspace.Id, "Backlog"), default);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Backlog");
        result.WorkspaceId.Should().Be(workspace.Id);
        result.Position.Should().Be(4);
    }

    [Fact]
    public async Task AddLane_AssignsSequentialPositions()
    {
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new AddLaneCommandHandler(_db);

        var fourth = await handler.Handle(new AddLaneCommand(workspace.Id, "Review"), default);
        var fifth = await handler.Handle(new AddLaneCommand(workspace.Id, "Deploy"), default);

        fourth.Position.Should().Be(4);
        fifth.Position.Should().Be(5);
    }

    [Fact]
    public async Task RenameLane_UpdatesName()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];

        var handler = new RenameLaneCommandHandler(_db);
        var result = await handler.Handle(new RenameLaneCommand(todo.Id, "Inbox"), default);

        result.Id.Should().Be(todo.Id);
        result.Name.Should().Be("Inbox");

        var persisted = await _db.Lanes.FindAsync(todo.Id);
        persisted!.Name.Should().Be("Inbox");
    }

    [Fact]
    public async Task MoveLane_ReordersLanesCorrectly()
    {
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var done = lanes[2];

        // Move "Done" (pos 3) to position 1 → Done, To Do, Doing
        var handler = new MoveLaneCommandHandler(_db);
        await handler.Handle(new MoveLaneCommand(done.Id, 1), default);

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
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];

        // Move "To Do" (pos 1) to position 3 → Doing, Done, To Do
        var handler = new MoveLaneCommandHandler(_db);
        await handler.Handle(new MoveLaneCommand(todo.Id, 3), default);

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
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var doing = lanes[1];

        var handler = new RemoveLaneCommandHandler(_db);
        await handler.Handle(new RemoveLaneCommand(doing.Id), default);

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
        var (_, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes[0];
        await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(todo.Id, "A task"), default);

        var handler = new RemoveLaneCommandHandler(_db);
        var act = async () => await handler.Handle(new RemoveLaneCommand(todo.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not empty*");
    }
}
