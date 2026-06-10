using Bishop.Life.Core.Google;
using FluentAssertions;
using System.Runtime.Versioning;

namespace Bishop.Life.Tests.Google;

[SupportedOSPlatform("windows")]
public class GoogleTokenStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsRefreshToken()
    {
        if (!OperatingSystem.IsWindows()) return; // DPAPI is Windows-only.
        using var dir = new TempDir();
        var store = new GoogleTokenStore(dir.FilePath("google-token.json"));

        store.SaveRefreshToken("1//refresh-token-payload");

        store.Exists().Should().BeTrue();
        store.LoadRefreshToken().Should().Be("1//refresh-token-payload");
    }

    [Fact]
    public void LoadRefreshToken_FileMissing_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var store = new GoogleTokenStore(dir.FilePath("google-token.json"));

        store.LoadRefreshToken().Should().BeNull();
    }

    [Fact]
    public void SaveRefreshToken_DoesNotWriteTokenInPlaintext()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var path = dir.FilePath("google-token.json");
        var store = new GoogleTokenStore(path);

        store.SaveRefreshToken("super-secret-refresh-token-value");

        var onDisk = File.ReadAllText(path);
        onDisk.Should().NotContain("super-secret-refresh-token-value");
    }

    [Fact]
    public void SaveRefreshToken_OverwritesPriorToken()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var store = new GoogleTokenStore(dir.FilePath("google-token.json"));

        store.SaveRefreshToken("old");
        store.SaveRefreshToken("new");

        store.LoadRefreshToken().Should().Be("new");
    }
}
