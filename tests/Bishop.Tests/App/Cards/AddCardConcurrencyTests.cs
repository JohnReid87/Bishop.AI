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

    [Fact]
    public async Task AddCard_ExhaustsRetries_ThrowsDbUpdateExceptionAfterFiveAttempts()
    {
        // Arrange — factory always raises a card-number UNIQUE conflict; safety rail at >20
        // prevents an infinite loop caused by the attempt-- mutant (would never reach MaxNumberMintRetries).
        var conflictEx = new DbUpdateException("conflict", new Exception("UNIQUE constraint failed: Cards.Number"));
        var factory = new CountingConflictingFactory(conflictEx);
        var handler = new AddCardCommandHandler(factory);

        // Act
        var act = () => handler.Handle(new AddCardCommand(Guid.NewGuid(), SystemLaneNames.ToDo, "X"), default);

        // Assert — original exception re-thrown (not NullReferenceException from skipped assignment,
        // not OperationCanceledException from the safety rail triggered by an infinite loop)
        await act.Should().ThrowAsync<DbUpdateException>();
        factory.Calls.Should().Be(5, "MaxNumberMintRetries is 5; handler must not attempt a 6th time");
    }

    [Fact]
    public async Task AddCard_NonCardNumberUniqueConstraint_PropagatesImmediately()
    {
        // Arrange — UNIQUE violation on a column other than Cards.Number must not be retried
        var conflictEx = new DbUpdateException("conflict", new Exception("UNIQUE constraint failed: Workspaces.Name"));
        var factory = new CountingConflictingFactory(conflictEx);
        var handler = new AddCardCommandHandler(factory);

        // Act
        var act = () => handler.Handle(new AddCardCommand(Guid.NewGuid(), SystemLaneNames.ToDo, "X"), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        factory.Calls.Should().Be(1, "non-card-number UNIQUE conflicts must not be retried");
    }

    [Fact]
    public async Task AddCard_ForeignKeyViolation_PropagatesImmediately()
    {
        // Arrange — non-UNIQUE exception with "Cards.Number" in the message must not be caught by the retry guard
        var conflictEx = new DbUpdateException("conflict", new Exception("FOREIGN KEY constraint failed: Cards.Number"));
        var factory = new CountingConflictingFactory(conflictEx);
        var handler = new AddCardCommandHandler(factory);

        // Act
        var act = () => handler.Handle(new AddCardCommand(Guid.NewGuid(), SystemLaneNames.ToDo, "X"), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        factory.Calls.Should().Be(1, "non-UNIQUE exceptions must propagate without retry");
    }

    private sealed class CountingConflictingFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly Exception _exception;
        private int _calls;

        public int Calls => _calls;

        public CountingConflictingFactory(Exception exception) => _exception = exception;

        public BishopDbContext CreateDbContext()
        {
            // Safety rail: terminate an infinite loop caused by a decrement mutant before
            // the test hangs. The test asserts DbUpdateException, so OperationCanceledException
            // from here correctly kills the mutant.
            if (++_calls > 20)
                throw new OperationCanceledException("Test guard: retry loop did not terminate as expected");
            throw _exception;
        }
    }

    private sealed class FileDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;

        public FileDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;

        public BishopDbContext CreateDbContext() => new(_options);
    }
}
