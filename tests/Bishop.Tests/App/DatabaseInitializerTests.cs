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

    private BishopDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options);

    private BishopDbContext CreateDbContextWithNoMigrations() =>
        new(new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}",
                opts => opts.MigrationsAssembly(typeof(DatabaseInitializerTests).Assembly.GetName().Name!))
            .Options);

    private BishopDbContext CreateDbContextWithWalThrowingInterceptor() =>
        new(new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .AddInterceptors(new WalPragmaThrowingInterceptor())
            .Options);

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

    [Fact]
    public Task StopAsync_CompletesSuccessfullySynchronously()
    {
        // Arrange
        using var db = CreateDbContext();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        using var db = CreateDbContext();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        using var db = CreateDbContextWithNoMigrations();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        using var db = CreateDbContext();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        using var db = CreateDbContext();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        using var db = CreateDbContextWithWalThrowingInterceptor();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

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
        using var db = CreateDbContext();
        var tagSeeder = Substitute.For<IDefaultTagSeeder>();
        var sut = new DatabaseInitializer(db, tagSeeder);

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
        using var db = CreateDbContextWithNoMigrations();
        var sut = new DatabaseInitializer(db, Substitute.For<IDefaultTagSeeder>());

        // Act
        await sut.StartAsync(default);

        // Assert — DB file was created (proving StartAsync passed IsStampCurrent's early-return),
        // and stamp is unchanged (WriteStampAsync returned early with no applied migrations)
        File.Exists(_tempDbPath).Should().BeTrue(
            "the DB connection must have been opened, proving IsStampCurrent returned false");
        File.ReadAllText(_stampPath).Should().Be(existingStamp,
            "WriteStampAsync should not overwrite the stamp when no migrations have been applied");
    }
}
