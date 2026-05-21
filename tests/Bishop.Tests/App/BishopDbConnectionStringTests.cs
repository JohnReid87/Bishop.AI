using Bishop.App;
using FluentAssertions;

namespace Bishop.Tests.App;

public sealed class BishopDbConnectionStringTests : IDisposable
{
    private readonly string? _originalBishopDb;
    private readonly string? _originalAppData;
    private readonly string _tempAppData;

    public BishopDbConnectionStringTests()
    {
        _originalBishopDb = Environment.GetEnvironmentVariable("BISHOP_DB");
        _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        _tempAppData = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempAppData);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BISHOP_DB", _originalBishopDb);
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
        if (Directory.Exists(_tempAppData))
            Directory.Delete(_tempAppData, recursive: true);
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsSet_ReturnsEnvVarConnectionString()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), "test_bishop.db");
        Environment.SetEnvironmentVariable("BISHOP_DB", dbPath);

        // Act
        var result = BishopDbConnectionString.Resolve();

        // Assert
        result.Should().Be($"Data Source={dbPath}");
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsEmptyString_ReturnsAppDataConnectionString()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_DB", "");
        Environment.SetEnvironmentVariable("APPDATA", _tempAppData);

        // Act
        var result = BishopDbConnectionString.Resolve();

        // Assert
        var expected = Path.Combine(_tempAppData, "Bishop.AI", "bishop.db");
        result.Should().Be($"Data Source={expected}");
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsNotSet_ReturnsAppDataConnectionString()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_DB", null);
        Environment.SetEnvironmentVariable("APPDATA", _tempAppData);

        // Act
        var result = BishopDbConnectionString.Resolve();

        // Assert
        var expected = Path.Combine(_tempAppData, "Bishop.AI", "bishop.db");
        result.Should().Be($"Data Source={expected}");
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsNotSet_EnsuresDirectoryExists()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_DB", null);
        Environment.SetEnvironmentVariable("APPDATA", _tempAppData);

        // Act
        BishopDbConnectionString.Resolve();

        // Assert
        var dir = Path.Combine(_tempAppData, "Bishop.AI");
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void Resolve_WhenAppDataEnvVarIsNull_FallsBackToSpecialFolderApplicationData()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_DB", null);
        Environment.SetEnvironmentVariable("APPDATA", null);

        // Act
        var result = BishopDbConnectionString.Resolve();

        // Assert
        var specialFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var expectedDir = Path.Combine(specialFolder, "Bishop.AI");
        result.Should().Be($"Data Source={Path.Combine(expectedDir, "bishop.db")}");
        Directory.Exists(expectedDir).Should().BeTrue();
    }
}
