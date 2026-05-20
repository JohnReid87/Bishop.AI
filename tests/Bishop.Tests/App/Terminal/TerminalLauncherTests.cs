#pragma warning disable CA1416 // Windows-only; tests run on Windows
using Bishop.App.Terminal;
using FluentAssertions;
using System.Diagnostics;

namespace Bishop.Tests.App.Terminal;

public sealed class TerminalLauncherTests
{
    private readonly List<ProcessStartInfo> _started = [];

    private TerminalLauncher CreateSut(bool wtExists, bool pwshExists = false)
    {
        bool FileExists(string path) =>
            (wtExists && path.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase)) ||
            (pwshExists && path.EndsWith("pwsh.exe", StringComparison.OrdinalIgnoreCase));

        return new TerminalLauncher(FileExists, psi => _started.Add(psi));
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    [Fact]
    public void Launch_WtFound_ReturnsTrue()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);

        // Act
        var result = sut.Launch(@"C:\Repo", null, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Launch_WtNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        var result = sut.Launch(@"C:\Repo", null, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Launch_WtFound_StartsWtProcess()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert
        _started.Single().FileName.ToLowerInvariant().Should().EndWith("wt.exe");
    }

    [Fact]
    public void Launch_WtNotFound_FallsBackToPowerShell()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert
        _started.Single().FileName.Should().Be("powershell.exe");
    }

    [Fact]
    public void Launch_WithClaudeArgs_AppendsQuotedArgsSuffix()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", "code .", null);

        // Assert
        _started.Single().Arguments.Should().Contain("\"code .\"");
    }

    [Fact]
    public void Launch_WithModelId_AppendsModelFlag()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null, "claude-opus-4");

        // Assert
        _started.Single().Arguments.Should().Contain("--model claude-opus-4");
    }

    [Fact]
    public void Launch_WithModelIdAndClaudeArgs_AppendsBoth()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", "code .", null, "claude-opus-4");

        // Assert
        var args = _started.Single().Arguments;
        args.Should().Contain("--model claude-opus-4");
        args.Should().Contain("\"code .\"");
    }

    [Fact]
    public void Launch_WithoutModelId_NoModelFlag()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert
        _started.Single().Arguments.Should().NotContain("--model");
    }

    [Fact]
    public void Launch_SetsPathInProcessEnvironment()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert
        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Launch_WithSnap_WtFound_ReturnsTrue()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        // Act
        var result = sut.Launch(@"C:\Repo", null, snap);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Launch_WithSnap_WtNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        // Act
        var result = sut.Launch(@"C:\Repo", null, snap);

        // Assert
        result.Should().BeFalse();
    }

    // ── LaunchPlain ───────────────────────────────────────────────────────────

    [Fact]
    public void LaunchPlain_WtFound_ReturnsTrue()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);

        // Act
        var result = sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchPlain_WtNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        var result = sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void LaunchPlain_WtFound_PwshAvailable_UsesPwsh()
    {
        // Arrange
        var sut = CreateSut(wtExists: true, pwshExists: true);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        _started.Single().Arguments.Should().Contain("pwsh.exe");
    }

    [Fact]
    public void LaunchPlain_WtFound_PwshUnavailable_UsesPowerShell()
    {
        // Arrange
        var sut = CreateSut(wtExists: true, pwshExists: false);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        _started.Single().Arguments.Should().Contain("powershell.exe");
    }

    [Fact]
    public void LaunchPlain_WtNotFound_PwshAvailable_UsesPwshAsFallbackFileName()
    {
        // Arrange
        var sut = CreateSut(wtExists: false, pwshExists: true);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        _started.Single().FileName.Should().Be("pwsh.exe");
    }

    [Fact]
    public void LaunchPlain_WtNotFound_PwshUnavailable_UsesPowerShellFallback()
    {
        // Arrange
        var sut = CreateSut(wtExists: false, pwshExists: false);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        _started.Single().FileName.Should().Be("powershell.exe");
    }

    [Fact]
    public void LaunchPlain_SetsPathInProcessEnvironment()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LaunchPlain_WithSnap_WtFound_ReturnsTrue()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        // Act
        var result = sut.LaunchPlain(@"C:\Repo", snap);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchPlain_WithSnap_WtNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        // Act
        var result = sut.LaunchPlain(@"C:\Repo", snap);

        // Assert
        result.Should().BeFalse();
    }
}
