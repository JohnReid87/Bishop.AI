using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
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
}
