using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Cards;

public sealed class ClaimCardConcurrencyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public ClaimCardConcurrencyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bishop_claim_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new FileDbContextFactory(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
            if (File.Exists(path))
                File.Delete(path);
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.ArgAt<GetCardQuery>(0);
                using var db = _factory.CreateDbContext();
                return Task.FromResult<Card?>(db.Cards.Find(query.CardId));
            });
        return sender;
    }

    [Fact]
    public async Task ConcurrentClaim_OnSingleCard_ExactlyOneWinsAndOneSeesNoCard()
    {
        // Arrange — real file-backed SQLite (EF Core in-memory provider does not
        // simulate SQLite's RESERVED-lock semantics, so the race is invisible there).
        var workspaceName = $"ws-{Guid.NewGuid():N}"[..20];
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(workspaceName, $@"C:\{workspaceName}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        var todo = lanes.Single(l => l.Name == SystemLaneNames.ToDo);
        var doing = lanes.Single(l => l.Name == SystemLaneNames.Doing);
        var card = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, todo.Name, "Only card"), default);

        var handlerA = new ClaimCardCommandHandler(_factory, CreateSender());
        var handlerB = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act — push both handlers onto the thread pool so they race.
        var taskA = Task.Run(() => handlerA.Handle(new ClaimCardCommand(workspace.Id, SystemLaneNames.ToDo), default));
        var taskB = Task.Run(() => handlerB.Handle(new ClaimCardCommand(workspace.Id, SystemLaneNames.ToDo), default));
        var results = await Task.WhenAll(taskA, taskB);

        // Assert
        var winners = results.Where(r => r is not null).ToList();
        var losers = results.Where(r => r is null).ToList();
        winners.Should().HaveCount(1, "exactly one concurrent claim must take the only card");
        losers.Should().HaveCount(1, "the other concurrent claim must see the empty source lane");
        winners[0]!.Id.Should().Be(card.Id);
        winners[0]!.LaneName.Should().Be(doing.Name);

        await using var verify = _factory.CreateDbContext();
        var inDoing = await verify.Cards.CountAsync(c => c.LaneName == doing.Name);
        var inTodo = await verify.Cards.CountAsync(c => c.LaneName == todo.Name);
        inDoing.Should().Be(1, "the claimed card must end up in Doing exactly once");
        inTodo.Should().Be(0, "the source lane must be empty after the claim");
    }

    private sealed class FileDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;

        public FileDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;

        public BishopDbContext CreateDbContext() => new(_options);
    }
}
