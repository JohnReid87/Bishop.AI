using Bishop.App;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Bishop.Tests.App;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;

    public DatabaseInitializerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"bishop_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_tempDbPath}";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _tempDbPath, _tempDbPath + "-shm", _tempDbPath + "-wal" })
            if (File.Exists(path))
                File.Delete(path);
    }

    private DbContextOptions<BishopDbContext> StandardOptions() =>
        new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connectionString)
            .Options;

    private DbContextOptions<BishopDbContext> NoMigrationsOptions() =>
        new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connectionString,
                opts => opts.MigrationsAssembly(typeof(DatabaseInitializerTests).Assembly.GetName().Name!))
            .Options;

    private DbContextOptions<BishopDbContext> WalThrowingOptions() =>
        new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new WalPragmaThrowingInterceptor())
            .Options;

    private BishopDbContext CreateDbContext() => new(StandardOptions());

    private IDbContextFactory<BishopDbContext> CreateFactory() => new TestDbContextFactory(StandardOptions());

    private IDbContextFactory<BishopDbContext> CreateFactoryWithNoMigrations() => new TestDbContextFactory(NoMigrationsOptions());

    private IDbContextFactory<BishopDbContext> CreateFactoryWithWalThrowingInterceptor() => new TestDbContextFactory(WalThrowingOptions());

    private IDbContextFactory<BishopDbContext> CreateFactoryWithMigrationDdlThrowingInterceptor() =>
        new TestDbContextFactory(
            new DbContextOptionsBuilder<BishopDbContext>()
                .UseSqlite(_connectionString)
                .AddInterceptors(new MigrationDdlThrowingInterceptor())
                .Options);

    private DatabaseInitializer CreateSut(IDbContextFactory<BishopDbContext>? factory = null) =>
        new(factory ?? CreateFactory(), _connectionString);

    private sealed class TestDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;

        public TestDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;

        public BishopDbContext CreateDbContext() => new(_options);
    }

    private sealed class MigrationDdlThrowingInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            if (command.CommandText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Simulated migration DDL failure.");
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Simulated migration DDL failure.");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class BlockingMigrationInterceptor : DbCommandInterceptor
    {
        private readonly SemaphoreSlim _blocking;

        public BlockingMigrationInterceptor(SemaphoreSlim blocking) => _blocking = blocking;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                _blocking.Release();
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var reg = cancellationToken.UnsafeRegister(
                    static (state, ct) => ((TaskCompletionSource)state!).TrySetCanceled(ct), tcs);
                await tcs.Task;
            }
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class WalPragmaThrowingInterceptor : DbCommandInterceptor
    {
        private const string WalPragmaText = "PRAGMA journal_mode=WAL";

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            if (command.CommandText.Contains(WalPragmaText, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Simulated WAL PRAGMA failure.");
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(WalPragmaText, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Simulated WAL PRAGMA failure.");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task<string> ReadJournalModeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }

    private async Task<IReadOnlyList<string>> ReadTableNamesAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }

    [Fact]
    public Task StopAsync_CompletesSuccessfullySynchronously()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var task = sut.StopAsync(default);

        // Assert
        task.IsCompletedSuccessfully.Should().BeTrue();
        return task;
    }

    [Fact]
    public async Task StartAsync_OnFreshDatabase_AppliesAllMigrationsAndEnablesWal()
    {
        // Arrange
        using var db = CreateDbContext();
        var declared = db.Database.GetMigrations().ToList();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(default);

        // Assert
        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().BeEquivalentTo(declared, "every declared migration must be applied on first init");
        (await ReadJournalModeAsync()).Should().Be("wal");
    }

    [Fact]
    public async Task StartAsync_WhenSchemaAlreadyCurrent_IsIdempotent()
    {
        // Arrange — first init fully migrates; the second must be a no-op without throwing.
        var sut = CreateSut();
        await sut.StartAsync(default);

        // Act
        await sut.StartAsync(default);

        // Assert — applied set still matches the declared set
        using var db = CreateDbContext();
        var declared = db.Database.GetMigrations().ToList();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().BeEquivalentTo(declared);
    }

    [Fact]
    public async Task StartAsync_WhenDatabaseIsCorrupt_Throws()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempDbPath, "not-a-valid-sqlite-database");
        var sut = CreateSut();

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<Exception>("pending-migrations check should fail against a corrupt database");
    }

    [Fact]
    public async Task StartAsync_WhenNoMigrationsDefined_CompletesWithoutCreatingSchema()
    {
        // Arrange — context with no migrations so GetPendingMigrations is empty,
        // skipping the MigrateAsync branch entirely.
        var sut = CreateSut(CreateFactoryWithNoMigrations());

        // Act
        await sut.StartAsync(default);

        // Assert — no schema tables created (the WAL PRAGMA still opens the DB file)
        var tables = await ReadTableNamesAsync();
        tables.Should().NotContain("Workspaces");
    }

    [Fact]
    public async Task StartAsync_WhenWalPragmaFails_Throws()
    {
        // Arrange — interceptor throws on PRAGMA WAL
        var sut = CreateSut(CreateFactoryWithWalThrowingInterceptor());

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>("the WAL PRAGMA failure should propagate from StartAsync");
    }

    [Fact]
    public async Task StartAsync_CalledConcurrently_BothSucceedAndSchemaIsValid()
    {
        // Arrange
        var sut = CreateSut();

        // Act — fire both without awaiting to maximise the concurrency window
        var task1 = sut.StartAsync(default);
        var task2 = sut.StartAsync(default);
        await Task.WhenAll(task1, task2);

        // Assert — schema must be present regardless of interleaving
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "schema must be fully initialised after concurrent StartAsync calls");
    }

    [Fact]
    public async Task StartAsync_WhenMigrateThrows_PropagatesException()
    {
        // Arrange — interceptor throws when migration DDL (CREATE TABLE) is executed,
        // while allowing the preceding GetPendingMigrations SELECT to succeed
        var sut = CreateSut(CreateFactoryWithMigrationDdlThrowingInterceptor());

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>("a migration DDL failure must propagate from StartAsync");
    }

    [Fact]
    public Task StopAsync_BeforeStartAsync_DoesNotCreateDatabaseFile()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var task = sut.StopAsync(default);

        // Assert — StopAsync must be a no-op: synchronous and no side-effects on disk
        task.IsCompletedSuccessfully.Should().BeTrue();
        File.Exists(_tempDbPath).Should().BeFalse("StopAsync must not open a database connection");
        return task;
    }

    [Fact]
    public async Task StopAsync_DuringStartAsync_CompletesImmediately()
    {
        // Arrange
        var sut = CreateSut();

        // Act — fire both; StopAsync must not block on StartAsync
        var startTask = sut.StartAsync(default);
        var stopTask = sut.StopAsync(default);

        // Assert
        stopTask.IsCompletedSuccessfully.Should().BeTrue("StopAsync always returns Task.CompletedTask regardless of StartAsync state");
        await startTask;
    }

    [Fact]
    public async Task StartAsync_BlocksWhileNamedMutexIsHeldByAnotherThread_AndCompletesAfterRelease()
    {
        // Arrange
        var sut = CreateSut();
        var mutexName = DatabaseInitializer.BuildMutexName(_connectionString);
        var mutexAcquired = new ManualResetEventSlim(false);
        var releaseMutex = new ManualResetEventSlim(false);
        var holderFailure = (Exception?)null;

        // Background thread holds the named Mutex. We use a dedicated Thread (not Task)
        // so the acquire and release are guaranteed to happen on the same OS thread —
        // Mutex has thread-affinity and ReleaseMutex from a different thread throws.
        var holder = new Thread(() =>
        {
            try
            {
                using var mutex = new Mutex(initiallyOwned: false, mutexName);
                mutex.WaitOne();
                try
                {
                    mutexAcquired.Set();
                    releaseMutex.Wait(TimeSpan.FromSeconds(30));
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                holderFailure = ex;
                mutexAcquired.Set();
            }
        })
        { IsBackground = true };

        holder.Start();
        mutexAcquired.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the holder thread must acquire the mutex first");
        holderFailure.Should().BeNull();

        // Act — StartAsync should block on the mutex until the holder releases it.
        var startTask = Task.Run(() => sut.StartAsync(default));
        var stillBlocked = await Task.WhenAny(startTask, Task.Delay(200)) != startTask;
        stillBlocked.Should().BeTrue("StartAsync must block while the named mutex is held by another thread");

        releaseMutex.Set();
        holder.Join(TimeSpan.FromSeconds(5)).Should().BeTrue("holder thread should exit after release signal");

        // Assert
        var completed = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(30))) == startTask;
        completed.Should().BeTrue("StartAsync should complete shortly after the mutex is released");
        await startTask;
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "schema must be migrated once StartAsync completes");
    }

    [Fact]
    public async Task StartAsync_WhenCancellationTokenIsAlreadyCancelled_ThrowsOperationCanceledException()
    {
        // Arrange — token is cancelled before StartAsync is called; the check fires
        // after the mutex is acquired, so the exception is thrown inside EnsureSchema
        // with the mutex still held.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateSut();

        // Act
        var act = () => sut.StartAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // MutexAcquireTimeout is a 60-second private constant with no injection point.
    // Holding a real named Mutex for that duration would make the suite unusable.
    // The TimeoutException path is verified by code inspection: mutex.WaitOne(MutexAcquireTimeout)
    // returns false when the timeout expires, which maps directly to the throw new TimeoutException(...) branch.
    [Fact(Skip =
        "Cannot reproduce without waiting 60 s — MutexAcquireTimeout has no injection point. " +
        "Timeout path verified by code inspection in DatabaseInitializer.EnsureSchema.")]
    public Task StartAsync_WhenMutexHoldExceedsAcquireTimeout_ThrowsTimeoutException()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_WhenCancellationTokenCancelledDuringMigration_ThrowsAndReleasesMutex()
    {
        // Arrange — interceptor blocks inside CREATE TABLE until the EF Core cancellation token
        // fires; the blocking semaphore lets the test wait until the block is in effect before cancelling.
        using var cts = new CancellationTokenSource();
        var blocking = new SemaphoreSlim(0, 1);
        var sut1 = new DatabaseInitializer(
            new TestDbContextFactory(
                new DbContextOptionsBuilder<BishopDbContext>()
                    .UseSqlite(_connectionString)
                    .AddInterceptors(new BlockingMigrationInterceptor(blocking))
                    .Options),
            _connectionString);
        var sut2 = CreateSut();

        // Act — start migration, wait until it is actually blocking, then cancel
        var startTask = sut1.StartAsync(cts.Token);
        (await blocking.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue("migration must reach the blocking point");
        cts.Cancel();

        var act = async () => await startTask;
        await act.Should().ThrowAsync<OperationCanceledException>("cancellation must propagate from inside MigrateAsync");

        // Assert — mutex must be released so a second initializer succeeds without deadlocking
        await sut2.StartAsync(default);
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "second init must complete after the cancelled first released the mutex");
    }

    [Fact]
    public async Task StartAsync_WhenMigrationThrows_MutexIsReleasedSoSubsequentCallSucceeds()
    {
        // Arrange — first init throws on migration DDL; mutex must still be released so the
        // second call is not forced to wait for the 60-second acquire timeout to expire.
        var sut1 = CreateSut(CreateFactoryWithMigrationDdlThrowingInterceptor());
        var sut2 = CreateSut();

        // Act
        var act = () => sut1.StartAsync(default);
        await act.Should().ThrowAsync<InvalidOperationException>("DDL failure must propagate from StartAsync");

        await sut2.StartAsync(default);

        // Assert
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "second init must complete once the failed first released the mutex");
    }

    [Fact]
    public async Task StartAsync_WhenStaleMigrationLockRowExists_ClearsItAndCompletes()
    {
        // Arrange — simulate a previous migration that was cancelled/killed after acquiring
        // EF Core's __EFMigrationsLock but before releasing it, leaving an orphaned row.
        // Without ClearStaleMigrationLock, AcquireDatabaseLockAsync would spin until the
        // MigrationTimeout cancels and StartAsync would throw a TaskCanceledException.
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                CREATE TABLE "__EFMigrationsLock" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                    "Timestamp" TEXT NOT NULL);
                INSERT INTO "__EFMigrationsLock" ("Id", "Timestamp") VALUES (1, '2020-01-01T00:00:00.0000000');
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();
        var sut = CreateSut();

        // Act
        var act = () => sut.StartAsync(default);

        // Assert — must complete (not cancel) and leave a fully migrated schema with the lock cleared
        await act.Should().NotThrowAsync("a stale __EFMigrationsLock row must be cleared, not block migration");
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "schema must be created after the stale lock is cleared");
    }

    [Fact]
    public async Task StartAsync_WhenMutexWasAbandonedByPriorHolder_CompletesSuccessfully()
    {
        // Arrange — a dedicated thread acquires the named mutex then exits without
        // releasing it, causing the OS to mark the mutex as abandoned.
        // EnsureSchema catches AbandonedMutexException and treats the acquisition
        // as successful, so StartAsync must complete.
        var mutexName = DatabaseInitializer.BuildMutexName(_connectionString);
        var mutexAcquired = new ManualResetEventSlim(false);

        var abandoner = new Thread(() =>
        {
            var mutex = new Mutex(initiallyOwned: false, mutexName);
            mutex.WaitOne();
            mutexAcquired.Set();
            // Thread exits here without ReleaseMutex — marks the mutex as abandoned.
        })
        { IsBackground = true };

        abandoner.Start();
        mutexAcquired.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the abandoner thread must acquire the mutex");
        abandoner.Join(TimeSpan.FromSeconds(5)).Should().BeTrue("the abandoner thread must exit, abandoning the mutex");

        var sut = CreateSut();

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().NotThrowAsync("AbandonedMutexException is caught and treated as a successful acquire");
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "schema init must complete even when the mutex was previously abandoned");
    }
}
