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

    [Fact]
    public async Task Handle_WithSkipAndTake_ReturnsPaginatedSubset()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var todoLane = lanes.First(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        for (var i = 0; i < 5; i++)
            await add.Handle(new AddCardCommand(wsId, todoLane.Name, $"Card {i + 1}"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var all = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, LaneName: todoLane.Name), default);
        var page = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, LaneName: todoLane.Name, Skip: 1, Take: 2), default);

        // Assert
        all.Should().HaveCount(5);
        page.Should().HaveCount(2);
        page[0].Title.Should().Be(all[1].Title);
        page[1].Title.Should().Be(all[2].Title);
    }

    [Fact]
    public async Task Handle_NoLaneFilter_OrdersBySystemLaneRank()
    {
        // Arrange — insert in reverse rank order so insertion-order cannot satisfy the assertion.
        var (wsId, _) = await CreateWorkspaceAsync();
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(wsId, SystemLaneNames.Done, "in-done"), default);
        await add.Handle(new AddCardCommand(wsId, SystemLaneNames.Doing, "in-doing"), default);
        await add.Handle(new AddCardCommand(wsId, SystemLaneNames.ToDo, "in-todo"), default);
        await add.Handle(new AddCardCommand(wsId, SystemLaneNames.Backlog, "in-backlog"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId), default);

        // Assert
        result.Select(c => c.LaneName).Should().Equal(
            SystemLaneNames.Backlog,
            SystemLaneNames.ToDo,
            SystemLaneNames.Doing,
            SystemLaneNames.Done);
    }

    [Fact]
    public async Task Handle_LaneFilter_OrdersByPositionAscending()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var todoLane = lanes.First(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        for (var i = 0; i < 4; i++)
            await add.Handle(new AddCardCommand(wsId, todoLane.Name, $"Card {i + 1}"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, LaneName: todoLane.Name), default);

        // Assert
        result.Select(c => c.Position).Should().BeInAscendingOrder();
        result.Select(c => c.Position).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Handle_SkipZero_ReturnsAllRecords()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var todoLane = lanes.First(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        for (var i = 0; i < 3; i++)
            await add.Handle(new AddCardCommand(wsId, todoLane.Name, $"Card {i + 1}"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, LaneName: todoLane.Name, Skip: 0), default);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_TakeMaxValue_ReturnsAllRecords()
    {
        // Arrange
        var (wsId, lanes) = await CreateWorkspaceAsync();
        var todoLane = lanes.First(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        for (var i = 0; i < 3; i++)
            await add.Handle(new AddCardCommand(wsId, todoLane.Name, $"Card {i + 1}"), default);
        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId, LaneName: todoLane.Name, Take: int.MaxValue), default);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_NoLaneFilter_NonSystemLaneSortsAfterDone()
    {
        // Arrange — add a Done card via handler, then insert a card with a non-system lane name
        // directly via DbContext (AddCardCommandHandler rejects non-system lane names).
        // Position=0 on the custom card means it would sort BEFORE Done if both cards receive the
        // same rank, which is exactly what happens when the Done-branch ternary is mutated to
        // always return 3 or always return int.MaxValue instead of the correct rank.
        var (wsId, _) = await CreateWorkspaceAsync();
        await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(wsId, SystemLaneNames.Done, "done-card"), default);

        await using var db = _factory.CreateDbContext();
        db.Cards.Add(new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = wsId,
            LaneName = "CustomLane",
            Title = "custom-lane-card",
            Number = 9999,
            Position = 0,
        });
        await db.SaveChangesAsync();

        var handler = new ListCardsByWorkspaceQueryHandler(_factory);

        // Act
        var result = await handler.Handle(new ListCardsByWorkspaceQuery(wsId), default);

        // Assert — Done (rank 3) must appear before CustomLane (rank int.MaxValue).
        result.Should().HaveCount(2);
        result[0].LaneName.Should().Be(SystemLaneNames.Done);
        result[1].LaneName.Should().Be("CustomLane");
    }
}
