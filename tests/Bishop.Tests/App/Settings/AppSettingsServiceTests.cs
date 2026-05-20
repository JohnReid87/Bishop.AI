using Bishop.App.Settings;
using Bishop.Data;
using FluentAssertions;

namespace Bishop.Tests.App.Settings;

public sealed class AppSettingsServiceTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly AppSettingsService _sut;

    public AppSettingsServiceTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _sut = new AppSettingsService(_db);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        // Act
        var result = await _sut.GetAsync("missing-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        // Arrange
        await _sut.SetAsync("theme", "dark");

        // Act
        var result = await _sut.GetAsync("theme");

        // Assert
        result.Should().Be("dark");
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingKey()
    {
        // Arrange
        await _sut.SetAsync("theme", "dark");

        // Act
        await _sut.SetAsync("theme", "light");

        // Assert
        var result = await _sut.GetAsync("theme");
        result.Should().Be("light");
    }

    [Fact]
    public async Task SetAsync_StoresMultipleKeysIndependently()
    {
        // Arrange
        await _sut.SetAsync("theme", "dark");
        await _sut.SetAsync("model", "claude-opus-4-7");

        // Act
        var theme = await _sut.GetAsync("theme");
        var model = await _sut.GetAsync("model");

        // Assert
        theme.Should().Be("dark");
        model.Should().Be("claude-opus-4-7");
    }
}
