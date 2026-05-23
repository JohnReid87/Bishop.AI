using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Lanes;

public sealed class LaneHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public LaneHandlerTests(DbFixture fixture) => _factory = fixture.Factory;

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task ListLanesByWorkspace_ReturnsFourSystemLanesInOrder()
    {
        var name = U("Seeded");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);

        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);

        lanes.Should().HaveCount(4);
        lanes.Select(l => l.Name).Should().Equal("Backlog", "To Do", "Doing", "Done");
        lanes.Select(l => l.Position).Should().Equal(1, 2, 3, 4);
    }
}
