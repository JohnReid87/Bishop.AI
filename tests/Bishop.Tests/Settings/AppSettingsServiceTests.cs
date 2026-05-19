using Bishop.App.Settings;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Settings;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BishopDbContext _db;
    private readonly AppSettingsService _sut;

    public AppSettingsServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new BishopDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new AppSettingsService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        var result = await _sut.GetAsync("missing-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        await _sut.SetAsync("theme", "dark");

        var result = await _sut.GetAsync("theme");

        result.Should().Be("dark");
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingKey()
    {
        await _sut.SetAsync("theme", "dark");

        await _sut.SetAsync("theme", "light");

        var result = await _sut.GetAsync("theme");
        result.Should().Be("light");
    }

    [Fact]
    public async Task SetAsync_StoresMultipleKeysIndependently()
    {
        await _sut.SetAsync("theme", "dark");
        await _sut.SetAsync("model", "claude-opus-4-7");

        var theme = await _sut.GetAsync("theme");
        var model = await _sut.GetAsync("model");

        theme.Should().Be("dark");
        model.Should().Be("claude-opus-4-7");
    }
}
