using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class SetCardCommitCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public SetCardCommitCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _connection = fixture.Connection;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Guid> CreateCardAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        var todo = lanes.Single(l => l.Name == "To Do");
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, todo.Name, "Title"), default);
        return card.Id;
    }

    [Fact]
    public async Task Handle_SetsCommitHashAndBranchName()
    {
        // Arrange
        var cardId = await CreateCardAsync();
        var sut = new SetCardCommitCommandHandler(_factory, TimeProvider.System);

        // Act
        await sut.Handle(new SetCardCommitCommand(cardId, "abc1234", "feature/my-branch"), default);

        // Assert
        var saved = await _db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        saved.CommitHash.Should().Be("abc1234");
        saved.BranchName.Should().Be("feature/my-branch");
    }

    [Fact]
    public async Task Handle_Throws_WhenCardDoesNotExist()
    {
        // Arrange
        var sut = new SetCardCommitCommandHandler(_factory, TimeProvider.System);

        // Act
        var act = () => sut.Handle(new SetCardCommitCommand(Guid.NewGuid(), "abc1234", "branch"), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
