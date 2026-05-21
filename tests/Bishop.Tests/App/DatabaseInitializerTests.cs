using Bishop.App;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public Task StopAsync_ReturnsCompletedTask()
    {
        // Arrange
        using var db = CreateDbContext();
        var sut = new DatabaseInitializer(db);

        // Act
        var task = sut.StopAsync(default);

        // Assert
        task.Should().BeSameAs(Task.CompletedTask);
        return task;
    }

    [Fact]
    public async Task StartAsync_WhenStampIsCurrent_DoesNotThrow()
    {
        // Arrange
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        File.WriteAllText(_stampPath, latestMigration);
        var sut = new DatabaseInitializer(db);

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
        var sut = new DatabaseInitializer(db);

        // Act
        await sut.StartAsync(default);

        // Assert
        File.Exists(_stampPath).Should().BeTrue();
        File.ReadAllText(_stampPath).Trim().Should().Be(latestMigration);
        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().NotBeEmpty("schema should have been created by migrations");
    }

    [Fact]
    public async Task StartAsync_WhenStampIsStale_RunsMigrationsAndUpdatesStamp()
    {
        // Arrange
        File.WriteAllText(_stampPath, "20000101000000_OldMigration");
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        var sut = new DatabaseInitializer(db);

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
        var sut = new DatabaseInitializer(db);

        // Act
        var act = () => sut.StartAsync(default);

        // Assert
        await act.Should().ThrowAsync<Exception>("pending-migrations check should fail against a corrupt database");
    }
}
