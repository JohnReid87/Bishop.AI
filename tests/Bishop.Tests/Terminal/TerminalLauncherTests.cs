#pragma warning disable CA1416 // Windows-only; tests run on Windows
using Bishop.App.Terminal;
using FluentAssertions;
using System.Diagnostics;

namespace Bishop.Tests.Terminal;

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
        var sut = CreateSut(wtExists: true);

        var result = sut.Launch(@"C:\Repo", null, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void Launch_WtNotFound_ReturnsFalse()
    {
        var sut = CreateSut(wtExists: false);

        var result = sut.Launch(@"C:\Repo", null, null);

        result.Should().BeFalse();
    }

    [Fact]
    public void Launch_WtFound_StartsWtProcess()
    {
        var sut = CreateSut(wtExists: true);

        sut.Launch(@"C:\Repo", null, null);

        _started.Single().FileName.ToLowerInvariant().Should().EndWith("wt.exe");
    }

    [Fact]
    public void Launch_WtNotFound_FallsBackToPowerShell()
    {
        var sut = CreateSut(wtExists: false);

        sut.Launch(@"C:\Repo", null, null);

        _started.Single().FileName.Should().Be("powershell.exe");
    }

    [Fact]
    public void Launch_WithClaudeArgs_AppendsQuotedArgsSuffix()
    {
        var sut = CreateSut(wtExists: false);

        sut.Launch(@"C:\Repo", "code .", null);

        _started.Single().Arguments.Should().Contain("\"code .\"");
    }

    [Fact]
    public void Launch_WithModelId_AppendsModelFlag()
    {
        var sut = CreateSut(wtExists: false);

        sut.Launch(@"C:\Repo", null, null, "claude-opus-4");

        _started.Single().Arguments.Should().Contain("--model claude-opus-4");
    }

    [Fact]
    public void Launch_WithModelIdAndClaudeArgs_AppendsBoth()
    {
        var sut = CreateSut(wtExists: false);

        sut.Launch(@"C:\Repo", "code .", null, "claude-opus-4");

        var args = _started.Single().Arguments;
        args.Should().Contain("--model claude-opus-4");
        args.Should().Contain("\"code .\"");
    }

    [Fact]
    public void Launch_WithoutModelId_NoModelFlag()
    {
        var sut = CreateSut(wtExists: false);

        sut.Launch(@"C:\Repo", null, null);

        _started.Single().Arguments.Should().NotContain("--model");
    }

    [Fact]
    public void Launch_SetsPathInProcessEnvironment()
    {
        var sut = CreateSut(wtExists: false);

        sut.Launch(@"C:\Repo", null, null);

        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Launch_WithSnap_WtFound_ReturnsTrue()
    {
        var sut = CreateSut(wtExists: true);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.Launch(@"C:\Repo", null, snap);

        result.Should().BeTrue();
    }

    [Fact]
    public void Launch_WithSnap_WtNotFound_ReturnsFalse()
    {
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.Launch(@"C:\Repo", null, snap);

        result.Should().BeFalse();
    }

    // ── LaunchPlain ───────────────────────────────────────────────────────────

    [Fact]
    public void LaunchPlain_WtFound_ReturnsTrue()
    {
        var sut = CreateSut(wtExists: true);

        var result = sut.LaunchPlain(@"C:\Repo", null);

        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchPlain_WtNotFound_ReturnsFalse()
    {
        var sut = CreateSut(wtExists: false);

        var result = sut.LaunchPlain(@"C:\Repo", null);

        result.Should().BeFalse();
    }

    [Fact]
    public void LaunchPlain_WtFound_PwshAvailable_UsesPwsh()
    {
        var sut = CreateSut(wtExists: true, pwshExists: true);

        sut.LaunchPlain(@"C:\Repo", null);

        _started.Single().Arguments.Should().Contain("pwsh.exe");
    }

    [Fact]
    public void LaunchPlain_WtFound_PwshUnavailable_UsesPowerShell()
    {
        var sut = CreateSut(wtExists: true, pwshExists: false);

        sut.LaunchPlain(@"C:\Repo", null);

        _started.Single().Arguments.Should().Contain("powershell.exe");
    }

    [Fact]
    public void LaunchPlain_WtNotFound_PwshAvailable_UsesPwshAsFallbackFileName()
    {
        var sut = CreateSut(wtExists: false, pwshExists: true);

        sut.LaunchPlain(@"C:\Repo", null);

        _started.Single().FileName.Should().Be("pwsh.exe");
    }

    [Fact]
    public void LaunchPlain_WtNotFound_PwshUnavailable_UsesPowerShellFallback()
    {
        var sut = CreateSut(wtExists: false, pwshExists: false);

        sut.LaunchPlain(@"C:\Repo", null);

        _started.Single().FileName.Should().Be("powershell.exe");
    }

    [Fact]
    public void LaunchPlain_SetsPathInProcessEnvironment()
    {
        var sut = CreateSut(wtExists: false);

        sut.LaunchPlain(@"C:\Repo", null);

        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LaunchPlain_WithSnap_WtFound_ReturnsTrue()
    {
        var sut = CreateSut(wtExists: true);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.LaunchPlain(@"C:\Repo", snap);

        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchPlain_WithSnap_WtNotFound_ReturnsFalse()
    {
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.LaunchPlain(@"C:\Repo", snap);

        result.Should().BeFalse();
    }
}
