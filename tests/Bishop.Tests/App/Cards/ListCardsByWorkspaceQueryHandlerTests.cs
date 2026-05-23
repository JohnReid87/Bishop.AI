using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class ListCardsByWorkspaceQueryHandlerTests : IClassFixture<DbFixture>
{
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public ListCardsByWorkspaceQueryHandlerTests(DbFixture fixture) => _factory = fixture.Factory;

    private async Task<(Guid workspaceId, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceAsync()
    {
        var name = $"ws-{Guid.NewGuid():N}"[..20];
        var ws = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(ws.Id), default);
        return (ws.Id, lanes);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllCards()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(wsId, lanes[0].Name, "Alpha"), default);
        await add.Handle(new AddCardCommand(wsId, lanes[1].Name, "Beta"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId), default);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_TagFilter_ReturnsOnlyMatchingTag()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(wsId, lanes[0].Name, "Bug card", TagName: "bug"), default);
        await add.Handle(new AddCardCommand(wsId, lanes[0].Name, "Feature card", TagName: "feature"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, TagName: "bug"), default);

        // Assert
        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("bug");
    }

    [Fact]
    public async Task Handle_LaneFilter_ReturnsOnlyMatchingLane()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var todoLane = lanes.First(l => l.Name == "To Do");
        var doingLane = lanes.First(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(wsId, todoLane.Name, "Todo card"), default);
        await add.Handle(new AddCardCommand(wsId, doingLane.Name, "Doing card"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, LaneName: "Doing"), default);

        // Assert
        result.Should().HaveCount(1);
        result[0].LaneName.Should().Be("Doing");
    }

    [Fact]
    public async Task Handle_TagAndLaneFilter_AndCombinesFilters()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var todoLane = lanes.First(l => l.Name == "To Do");
        var doingLane = lanes.First(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(wsId, todoLane.Name, "Todo bug", TagName: "bug"), default);
        await add.Handle(new AddCardCommand(wsId, doingLane.Name, "Doing bug", TagName: "bug"), default);
        await add.Handle(new AddCardCommand(wsId, todoLane.Name, "Todo no-tag"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, TagName: "bug", LaneName: "To Do"), default);

        // Assert
        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("bug");
        result[0].LaneName.Should().Be("To Do");
    }

    [Fact]
    public async Task Handle_NonExistentTag_ReturnsEmpty()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(wsId, lanes[0].Name, "Some card"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, TagName: "does-not-exist"), default);

        // Assert
        result.Should().BeEmpty();
    }
}
