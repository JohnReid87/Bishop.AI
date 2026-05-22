using Bishop.App;
using FluentAssertions;

namespace Bishop.Tests.App;

public sealed class BishopStampPathTests : IDisposable
{
    private readonly string? _originalBishopStamp;
    private readonly string? _originalAppData;
    private readonly string _tempAppData;

    public BishopStampPathTests()
    {
        _originalBishopStamp = Environment.GetEnvironmentVariable("BISHOP_STAMP");
        _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        _tempAppData = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempAppData);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BISHOP_STAMP", _originalBishopStamp);
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
        if (Directory.Exists(_tempAppData))
            Directory.Delete(_tempAppData, recursive: true);
    }

    [Fact]
    public void Resolve_WhenBishopStampEnvVarIsSet_ReturnsEnvVarPath()
    {
        // Arrange
        var stampPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "custom_stamp");
        Environment.SetEnvironmentVariable("BISHOP_STAMP", stampPath);

        // Act
        var result = BishopStampPath.Resolve();

        // Assert
        result.Should().Be(stampPath);
    }

    [Fact]
    public void Resolve_WhenBishopStampEnvVarIsSet_EnsuresParentDirectoryExists()
    {
        // Arrange
        var parentDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var stampPath = Path.Combine(parentDir, "custom_stamp");
        Environment.SetEnvironmentVariable("BISHOP_STAMP", stampPath);

        try
        {
            // Act
            BishopStampPath.Resolve();

            // Assert
            Directory.Exists(parentDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(parentDir))
                Directory.Delete(parentDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_WhenBishopStampEnvVarIsEmptyString_ReturnsAppDataPath()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_STAMP", "");
        Environment.SetEnvironmentVariable("APPDATA", _tempAppData);

        // Act
        var result = BishopStampPath.Resolve();

        // Assert
        var expected = Path.Combine(_tempAppData, "Bishop.AI", "migration_stamp");
        result.Should().Be(expected);
    }

    [Fact]
    public void Resolve_WhenBishopStampEnvVarIsNotSet_ReturnsAppDataPath()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_STAMP", null);
        Environment.SetEnvironmentVariable("APPDATA", _tempAppData);

        // Act
        var result = BishopStampPath.Resolve();

        // Assert
        var expected = Path.Combine(_tempAppData, "Bishop.AI", "migration_stamp");
        result.Should().Be(expected);
    }

    [Fact]
    public void Resolve_WhenBishopStampEnvVarIsNotSet_EnsuresDirectoryExists()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_STAMP", null);
        Environment.SetEnvironmentVariable("APPDATA", _tempAppData);

        // Act
        BishopStampPath.Resolve();

        // Assert
        var dir = Path.Combine(_tempAppData, "Bishop.AI");
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void Resolve_WhenAppDataEnvVarIsNull_FallsBackToSpecialFolderApplicationData()
    {
        // Arrange
        Environment.SetEnvironmentVariable("BISHOP_STAMP", null);
        Environment.SetEnvironmentVariable("APPDATA", null);

        // Act
        var result = BishopStampPath.Resolve();

        // Assert
        var specialFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var expectedDir = Path.Combine(specialFolder, "Bishop.AI");
        result.Should().Be(Path.Combine(expectedDir, "migration_stamp"));
        Directory.Exists(expectedDir).Should().BeTrue();
    }
}
