using Bishop.App;
using Bishop.App.Tags;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using System.Data.Common;

namespace Bishop.Tests.App;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _stampPath;
    private readonly string? _originalStamp;

    public DatabaseInitializerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"bishop_test_{Guid.NewGuid():N}.db");
        _stampPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI",
            "migration_stamp");
        _originalStamp = File.Exists(_stampPath) ? File.ReadAllText(_stampPath) : null;
    }

    public void Dispose()
    {
        if (_originalStamp is not null)
            File.WriteAllText(_stampPath, _originalStamp);
        else if (File.Exists(_stampPath))
            File.Delete(_stampPath);

        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _tempDbPath, _tempDbPath + "-shm", _tempDbPath + "-wal" })
            if (File.Exists(path))
                File.Delete(path);
    }

    private DbContextOptions<BishopDbContext> StandardOptions() =>
        new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options;

    private DbContextOptions<BishopDbContext> NoMigrationsOptions() =>
        new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}",
                opts => opts.MigrationsAssembly(typeof(DatabaseInitializerTests).Assembly.GetName().Name!))
            .Options;

    private DbContextOptions<BishopDbContext> WalThrowingOptions() =>
        new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .AddInterceptors(new WalPragmaThrowingInterceptor())
            .Options;

    private BishopDbContext CreateDbContext() => new(StandardOptions());

    private IDbContextFactory<BishopDbContext> CreateFactory() => new TestDbContextFactory(StandardOptions());

    private IDbContextFactory<BishopDbContext> CreateFactoryWithNoMigrations() => new TestDbContextFactory(NoMigrationsOptions());

    private IDbContextFactory<BishopDbContext> CreateFactoryWithWalThrowingInterceptor() => new TestDbContextFactory(WalThrowingOptions());

    private IDbContextFactory<BishopDbContext> CreateFactoryWithMigrationDdlThrowingInterceptor() =>
        new TestDbContextFactory(
            new DbContextOptionsBuilder<BishopDbContext>()
                .UseSqlite($"Data Source={_tempDbPath}")
                .AddInterceptors(new MigrationDdlThrowingInterceptor())
                .Options);

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
        await using var connection = new SqliteConnection($"Data Source={_tempDbPath}");
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }

    private async Task<IReadOnlyList<string>> ReadTableNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_tempDbPath}");
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
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        var task = sut.StopAsync(default);

        // Assert
        task.IsCompletedSuccessfully.Should().BeTrue();
        return task;
    }

    [Fact]
    public async Task StartAsync_WhenStampIsCurrent_DoesNotThrow()
    {
        // Arrange
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        File.WriteAllText(_stampPath, latestMigration);
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert
        File.Exists(_tempDbPath).Should().BeFalse("migrations should not be invoked when stamp is current");
    }

    [Fact]
    public async Task StartAsync_WhenStampIsMissing_RunsMigrationsAndWritesStamp()
    {
        // Arrange
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert
        File.Exists(_stampPath).Should().BeTrue();
        File.ReadAllText(_stampPath).Trim().Should().Be(latestMigration);
        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().NotBeEmpty("schema should have been created by migrations");
        var journalMode = await ReadJournalModeAsync();
        journalMode.Should().Be("wal");
    }

    [Fact]
    public async Task StartAsync_WhenStampIsStale_RunsMigrationsAndUpdatesStamp()
    {
        // Arrange
        File.WriteAllText(_stampPath, "20000101000000_OldMigration");
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert
        File.ReadAllText(_stampPath).Trim().Should().Be(latestMigration);
        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().NotBeEmpty("migrations should have re-run due to stale stamp");
    }

    [Fact]
    public async Task StartAsync_WhenDatabaseIsCorrupt_Throws()
    {
        // Arrange
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        await File.WriteAllTextAsync(_tempDbPath, "not-a-valid-sqlite-database");
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<Exception>("pending-migrations check should fail against a corrupt database");
    }

    [Fact]
    public async Task StartAsync_WhenNoAppliedMigrations_DoesNotWriteStamp()
    {
        // Arrange — context with no migrations so GetAppliedMigrationsAsync returns empty,
        // exercising the early-return guard in WriteStampAsync
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var sut = new DatabaseInitializer(CreateFactoryWithNoMigrations(), Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert
        File.Exists(_stampPath).Should().BeFalse("WriteStampAsync should return early when no migrations have been applied");
    }

    [Fact]
    public async Task StartAsync_WhenStampFileIsUnreadable_Throws()
    {
        // Arrange — create stamp then lock it exclusively so File.ReadAllText throws IOException
        File.WriteAllText(_stampPath, "any_migration");
        await using var lockStream = new FileStream(_stampPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task StartAsync_WhenStampFileIsReadOnly_ThrowsOnWrite()
    {
        // Arrange — stale stamp so IsStampCurrent returns false; read-only so WriteStampAsync fails
        File.WriteAllText(_stampPath, "20000101000000_StaleStamp");
        File.SetAttributes(_stampPath, FileAttributes.ReadOnly);
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        var act = () => sut.StartAsync(default);

        try
        {
            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            File.SetAttributes(_stampPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task StartAsync_WhenWalPragmaFails_Throws()
    {
        // Arrange — delete stamp so IsStampCurrent returns false; interceptor throws on PRAGMA WAL
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var sut = new DatabaseInitializer(CreateFactoryWithWalThrowingInterceptor(), Substitute.For<IDefaultTagSeeder>());

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>("the WAL PRAGMA failure should propagate from StartAsync");
    }

    [Fact]
    public async Task StartAsync_InvokesTagSeederAfterSchemaIsReady()
    {
        // Arrange
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var tagSeeder = Substitute.For<IDefaultTagSeeder>();
        var sut = new DatabaseInitializer(CreateFactory(), tagSeeder);

        // Act
        await sut.StartAsync(default);

        // Assert
        await tagSeeder.Received(1).EnsureAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenNoMigrationsDefinedAndStampExists_ProceedsPastEarlyReturn()
    {
        // Arrange — IsStampCurrent returns false when GetMigrations().LastOrDefault() is null,
        // even if a stamp file exists, because the guard requires a non-null latest migration
        const string existingStamp = "20240101000000_OldMigration";
        File.WriteAllText(_stampPath, existingStamp);
        var sut = new DatabaseInitializer(CreateFactoryWithNoMigrations(), Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert — DB file was created (proving StartAsync passed IsStampCurrent's early-return),
        // and stamp is unchanged (WriteStampAsync returned early with no applied migrations)
        File.Exists(_tempDbPath).Should().BeTrue(
            "the DB connection must have been opened, proving IsStampCurrent returned false");
        File.ReadAllText(_stampPath).Should().Be(existingStamp,
            "WriteStampAsync should not overwrite the stamp when no migrations have been applied");
    }

    [Fact]
    public async Task StartAsync_CalledConcurrently_BothSucceedAndSchemaIsValid()
    {
        // Arrange
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act — fire both without awaiting to maximise the concurrency window
        var task1 = sut.StartAsync(default);
        var task2 = sut.StartAsync(default);
        await Task.WhenAll(task1, task2);

        // Assert — schema must be present and stamp must be written regardless of interleaving
        File.Exists(_stampPath).Should().BeTrue("both concurrent calls must leave the stamp in place");
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "schema must be fully initialised after concurrent StartAsync calls");
    }

    [Fact]
    public async Task StartAsync_WhenMigrateAsyncThrows_PropagatesException()
    {
        // Arrange — interceptor throws when migration DDL (CREATE TABLE) is executed,
        // while allowing the preceding GetPendingMigrationsAsync SELECT to succeed
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var sut = new DatabaseInitializer(CreateFactoryWithMigrationDdlThrowingInterceptor(), Substitute.For<IDefaultTagSeeder>());

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>("a migration DDL failure must propagate from StartAsync");
    }

    [Fact]
    public async Task StartAsync_WhenStampIsMissing_WorkspacesTableIsPresent()
    {
        // Arrange
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert
        var tables = await ReadTableNamesAsync();
        tables.Should().Contain("Workspaces", "the Workspaces table must be created by migrations on first init");
    }

    [Fact]
    public Task StopAsync_BeforeStartAsync_DoesNotCreateDatabaseFile()
    {
        // Arrange
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

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
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        var sut = new DatabaseInitializer(CreateFactory(), Substitute.For<IDefaultTagSeeder>());

        // Act — fire both; StopAsync must not block on StartAsync
        var startTask = sut.StartAsync(default);
        var stopTask = sut.StopAsync(default);

        // Assert
        stopTask.IsCompletedSuccessfully.Should().BeTrue("StopAsync always returns Task.CompletedTask regardless of StartAsync state");
        await startTask;
    }
}
