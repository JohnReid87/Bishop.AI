using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class RecordAutoRunFailureCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public RecordAutoRunFailureCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _connection = fixture.Connection;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Guid> CreateCardAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, todo.Name, "Title"), default);
        return card.Id;
    }

    [Fact]
    public async Task Handle_SetsLastAutoRunFailedAt()
    {
        // Arrange
        var cardId = await CreateCardAsync();
        var sut = new RecordAutoRunFailureCommandHandler(_factory, TimeProvider.System);

        // Act
        await sut.Handle(new RecordAutoRunFailureCommand(cardId), default);

        // Assert
        var saved = await _db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        saved.LastAutoRunFailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Throws_WhenCardDoesNotExist()
    {
        // Arrange
        var sut = new RecordAutoRunFailureCommandHandler(_factory, TimeProvider.System);

        // Act
        var act = () => sut.Handle(new RecordAutoRunFailureCommand(Guid.NewGuid()), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
