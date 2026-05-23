using Bishop.App.Lanes.ListLanesByWorkspace;
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
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public LaneHandlerTests(DbFixture fixture)
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
    public async Task CreateWorkspace_SeedsFourLanes()
    {
        // Arrange
        var name = U("Seeded");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new ListLanesByWorkspaceQueryHandler(_factory);

        // Act
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);

        // Assert
        lanes.Should().HaveCount(4);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task CreateWorkspace_SeedsSystemLanes()
    {
        // Arrange
        var name = U("Sys");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new ListLanesByWorkspaceQueryHandler(_factory);

        // Act
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);

        // Assert
        lanes.Should().AllSatisfy(l => l.IsSystem.Should().BeTrue());
    }

    [Fact]
    public async Task ListLanesByWorkspace_ReturnsOnlyLanesForThatWorkspace()
    {
        // Arrange
        var n1 = U("WS1");
        var n2 = U("WS2");
        var ws1 = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(n1, $@"C:\{n1}"), default);
        _ = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(n2, $@"C:\{n2}"), default);
        var handler = new ListLanesByWorkspaceQueryHandler(_factory);

        // Act
        var lanes = await handler.Handle(new ListLanesByWorkspaceQuery(ws1.Id), default);

        // Assert
        lanes.Should().HaveCount(4);
        lanes.Should().AllSatisfy(l => l.WorkspaceId.Should().Be(ws1.Id));
    }

    [Fact]
    public async Task ListLanesByWorkspace_ReflectsOutOfBandRename()
    {
        // Regression for card #5: a lane renamed via the CLI must surface on refresh
        // in the UI (the UI's long-lived DbContext was caching tracked entities).

        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var handler = new ListLanesByWorkspaceQueryHandler(_factory);
        var initial = await handler.Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        initial[0].Name.Should().Be("Backlog");

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
