using Bishop.App.Cards.AddCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Cards;

public sealed class AddCardConcurrencyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public AddCardConcurrencyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bishop_addcard_{Guid.NewGuid():N}.db");
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

    [Fact]
    public async Task ConcurrentAddCard_NoDuplicateNumbers()
    {
        // Arrange — real file-backed SQLite; in-memory does not model write serialization.
        var workspaceName = $"ws-{Guid.NewGuid():N}"[..20];
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(workspaceName, $@"C:\{workspaceName}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        var lane = lanes.Single(l => l.Name == SystemLaneNames.ToDo);

        const int concurrency = 8;
        var handlers = Enumerable.Range(0, concurrency)
            .Select(_ => new AddCardCommandHandler(_factory))
            .ToList();

        // Act — push all handlers onto the thread pool so they race.
        var tasks = handlers.Select((h, i) =>
            Task.Run(() => h.Handle(new AddCardCommand(workspace.Id, lane.Name, $"Card {i}"), default)));
        var cards = await Task.WhenAll(tasks);

        // Assert — all numbers are unique and sequential from 1..concurrency
        var numbers = cards.Select(c => c.Number).OrderBy(n => n).ToList();
        numbers.Should().OnlyHaveUniqueItems("concurrent adds must not mint the same card number");
        numbers.Should().Equal(Enumerable.Range(1, concurrency), "card numbers must be sequential with no gaps");
    }

    private sealed class FileDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;

        public FileDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;

        public BishopDbContext CreateDbContext() => new(_options);
    }
}
