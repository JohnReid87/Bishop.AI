using Bishop.App;
using FluentAssertions;

namespace Bishop.Tests.App;

public sealed class BishopDbConnectionStringTests : IDisposable
{
    private readonly string? _originalEnvVar;

    public BishopDbConnectionStringTests()
    {
        _originalEnvVar = Environment.GetEnvironmentVariable("BISHOP_DB");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BISHOP_DB", _originalEnvVar);
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsSet_ReturnsEnvVarConnectionString()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "test_bishop.db");
        Environment.SetEnvironmentVariable("BISHOP_DB", dbPath);

        var result = BishopDbConnectionString.Resolve();

        result.Should().Be($"Data Source={dbPath}");
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsNotSet_ReturnsAppDataConnectionString()
    {
        Environment.SetEnvironmentVariable("BISHOP_DB", null);

        var result = BishopDbConnectionString.Resolve();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI",
            "bishop.db");
        result.Should().Be($"Data Source={expected}");
    }

    [Fact]
    public void Resolve_WhenBishopDbEnvVarIsNotSet_EnsuresDirectoryExists()
    {
        Environment.SetEnvironmentVariable("BISHOP_DB", null);

        BishopDbConnectionString.Resolve();

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI");
        Directory.Exists(dir).Should().BeTrue();
    }
}
