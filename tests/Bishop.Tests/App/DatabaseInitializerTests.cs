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
        using var db = CreateDbContext();
        var sut = new DatabaseInitializer(db);

        var task = sut.StopAsync(default);

        task.IsCompleted.Should().BeTrue();
        return task;
    }

    [Fact]
    public async Task StartAsync_WhenStampIsCurrent_DoesNotThrow()
    {
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        File.WriteAllText(_stampPath, latestMigration);
        var sut = new DatabaseInitializer(db);

        var act = () => sut.StartAsync(default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_WhenStampIsMissing_RunsMigrationsAndWritesStamp()
    {
        if (File.Exists(_stampPath))
            File.Delete(_stampPath);
        using var db = CreateDbContext();
        var latestMigration = db.Database.GetMigrations().Last();
        var sut = new DatabaseInitializer(db);

        await sut.StartAsync(default);

        File.Exists(_stampPath).Should().BeTrue();
        File.ReadAllText(_stampPath).Trim().Should().Be(latestMigration);
    }
}
