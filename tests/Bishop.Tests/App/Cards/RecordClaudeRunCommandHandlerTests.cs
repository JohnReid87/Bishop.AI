using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class RecordClaudeRunCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public RecordClaudeRunCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _connection = fixture.Connection;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Card> CreateCardAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        var todo = lanes.Single(l => l.Name == "To Do");
        return await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, todo.Name, "Title"), default);
    }

    [Fact]
    public async Task Handle_SetsTotalsAndIncrementsRunCount_OnFirstRun()
    {
        // Arrange
        var card = await CreateCardAsync();
        var sut = new RecordClaudeRunCommandHandler(_factory);

        // Act
        await sut.Handle(new RecordClaudeRunCommand(card.Id, 8100, 2400), default);

        // Assert
        var saved = await _db.Cards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
        saved.TotalInputTokens.Should().Be(8100);
        saved.TotalOutputTokens.Should().Be(2400);
        saved.ClaudeRunCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SumsAcrossRetries_RatherThanOverwriting()
    {
        // Arrange
        var card = await CreateCardAsync();
        var sut = new RecordClaudeRunCommandHandler(_factory);

        // Act
        await sut.Handle(new RecordClaudeRunCommand(card.Id, 500, 200), default);
        await sut.Handle(new RecordClaudeRunCommand(card.Id, 300, 150), default);

        // Assert
        var saved = await _db.Cards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
        saved.TotalInputTokens.Should().Be(800);
        saved.TotalOutputTokens.Should().Be(350);
        saved.ClaudeRunCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Throws_WhenCardDoesNotExist()
    {
        var sut = new RecordClaudeRunCommandHandler(_factory);

        var act = () => sut.Handle(new RecordClaudeRunCommand(Guid.NewGuid(), 0, 0), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
