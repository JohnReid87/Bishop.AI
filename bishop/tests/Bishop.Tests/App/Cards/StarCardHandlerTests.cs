using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.StarCard;
using Bishop.App.Cards.UnstarCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class StarCardHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public StarCardHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private async Task<Card> CreateCardAsync()
    {
        var name = $"Test-{Guid.NewGuid():N}"[..20];
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, lanes[0].Name, "Focus me"), default);
    }

    [Fact]
    public async Task StarCard_SetsIsStarredTrue()
    {
        // Arrange
        var card = await CreateCardAsync();

        // Act
        var result = await new StarCardCommandHandler(_factory)
            .Handle(new StarCardCommand(card.Id), default);

        // Assert
        result.IsStarred.Should().BeTrue();
        (await _db.Cards.FindAsync(card.Id))!.IsStarred.Should().BeTrue();
    }

    [Fact]
    public async Task UnstarCard_SetsIsStarredFalse()
    {
        // Arrange
        var card = await CreateCardAsync();
        await new StarCardCommandHandler(_factory).Handle(new StarCardCommand(card.Id), default);
        _db.ChangeTracker.Clear();

        // Act
        var result = await new UnstarCardCommandHandler(_factory)
            .Handle(new UnstarCardCommand(card.Id), default);

        // Assert
        result.IsStarred.Should().BeFalse();
        (await _db.Cards.FindAsync(card.Id))!.IsStarred.Should().BeFalse();
    }

    [Fact]
    public async Task StarCard_NonexistentCard_Throws()
    {
        var handler = new StarCardCommandHandler(_factory);

        var act = () => handler.Handle(new StarCardCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
