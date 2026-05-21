#pragma warning disable CA1416 // Windows-only; tests run on Windows
using Bishop.App.Terminal;
using FluentAssertions;
using Microsoft.Win32;
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

        // Assert — verify the exact Windows Terminal alias path resolved by FindWindowsTerminal
        var expectedWt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        _started.Single().FileName.Should().Be(expectedWt);
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
    public void Launch_SetsPathToBuildFullPathResult()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert — PATH must match the merged registry PATH that BuildFullPath() computes
        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().Be(ExpectedFullPath());
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
    public void LaunchPlain_SetsPathToBuildFullPathResult()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert — PATH must match the merged registry PATH that BuildFullPath() computes
        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().Be(ExpectedFullPath());
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

    // ── LaunchCommand ─────────────────────────────────────────────────────────

    [Fact]
    public void LaunchCommand_WtFound_ReturnsTrue()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);

        // Act
        var result = sut.LaunchCommand(@"C:\Repo", "bishop", "work-next --max 10", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchCommand_WtFound_BuildsWtArgumentsWithCommandAndArgs()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", "work-next --tag test --max 5", null);

        // Assert
        _started.Single().Arguments.Should().Be(@"-d ""C:\Repo"" cmd.exe /k bishop work-next --tag test --max 5");
    }

    [Fact]
    public void LaunchCommand_WtNotFound_FallsBackToPowerShellWithCommand()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", "work-next --max 10", null);

        // Assert
        _started.Single().FileName.Should().Be("powershell.exe");
        _started.Single().Arguments.Should().Be("-NoExit -Command bishop work-next --max 10");
        _started.Single().WorkingDirectory.Should().Be(@"C:\Repo");
    }

    [Fact]
    public void LaunchCommand_WithNullArgs_DoesNotAppendTrailingSpace()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", null, null);

        // Assert
        _started.Single().Arguments.Should().Be("-NoExit -Command bishop");
    }

    [Fact]
    public void LaunchCommand_SetsPathInProcessEnvironment()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", "work-next", null);

        // Assert
        _started.Single().Environment.Should().ContainKey("PATH");
        _started.Single().Environment["PATH"].Should().NotBeNullOrEmpty();
    }

    // ── ArgumentException catch branches ─────────────────────────────────────

    [Fact]
    public void Launch_FileExistsThrowsArgumentExceptionForWtInPath_FallsBackToPowerShell()
    {
        // Exercises the catch(ArgumentException) in FindWindowsTerminal's PATH loop.
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        var sut = new TerminalLauncher(
            path =>
            {
                if (path == alias) return false;
                if (path.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("malformed path segment");
                return false;
            },
            psi => _started.Add(psi));

        sut.Launch(@"C:\Repo", null, null);

        _started.Single().FileName.Should().Be("powershell.exe");
    }

    [Fact]
    public void LaunchPlain_FileExistsThrowsArgumentExceptionForPwsh_FallsBackToPowerShell()
    {
        // Exercises the catch(ArgumentException) in HasPwsh's PATH loop.
        var sut = new TerminalLauncher(
            path =>
            {
                if (path.EndsWith("pwsh.exe", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("malformed path segment");
                return false;
            },
            psi => _started.Add(psi));

        sut.LaunchPlain(@"C:\Repo", null);

        _started.Single().FileName.Should().Be("powershell.exe");
    }

    // ── Null / empty workingDirectory and claudeArgs ──────────────────────────

    [Fact]
    public void Launch_NullWorkingDirectory_DoesNotThrow()
    {
        var sut = CreateSut(wtExists: false);
        var act = () => sut.Launch(null!, null, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void Launch_EmptyWorkingDirectory_DoesNotThrow()
    {
        var sut = CreateSut(wtExists: false);
        var act = () => sut.Launch(string.Empty, null, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void Launch_EmptyClaudeArgs_AppendsEmptyQuotedString()
    {
        var sut = CreateSut(wtExists: false);
        sut.Launch(@"C:\Repo", "", null);
        _started.Single().Arguments.Should().EndWith("claude \"\"");
    }

    [Fact]
    public void LaunchPlain_NullWorkingDirectory_DoesNotThrow()
    {
        var sut = CreateSut(wtExists: false);
        var act = () => sut.LaunchPlain(null!, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void LaunchPlain_EmptyWorkingDirectory_DoesNotThrow()
    {
        var sut = CreateSut(wtExists: false);
        var act = () => sut.LaunchPlain(string.Empty, null);
        act.Should().NotThrow();
    }

    // ── Parameterless constructor ─────────────────────────────────────────────

    [Fact]
    public void ParameterlessConstructor_DoesNotThrow()
    {
        // Verifies the default File.Exists / Process.Start wiring compiles and initialises.
        var act = () => _ = new TerminalLauncher();
        act.Should().NotThrow();
    }

    // ── SnapLater / ApplySnap ─────────────────────────────────────────────────
    // ApplySnap and the inner window-poll loop in SnapLater depend on real win32
    // window handles (EnumWindows, DwmGetWindowAttribute, SetWindowPos). They are
    // deliberately untested here: no real windows exist in a unit-test process, so
    // the background Task launched by SnapLater will time out harmlessly after 3 s
    // and return without snapping. The smoke tests below confirm the snap code path
    // is reached without crashing the test host.

    [Fact]
    public void Launch_WithSnap_WtFound_DoesNotThrow()
    {
        var sut = CreateSut(wtExists: true);
        var snap = new TerminalSnap(0, 0, 1280, 1440);
        var act = () => sut.Launch(@"C:\Repo", null, snap);
        act.Should().NotThrow();
    }

    [Fact]
    public void LaunchPlain_WithSnap_WtNotFound_DoesNotThrow()
    {
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);
        var act = () => sut.LaunchPlain(@"C:\Repo", snap);
        act.Should().NotThrow();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Replicates BuildFullPath() so tests can assert the exact PATH value set on PSI.
    // Returns "" when both registry sub-keys are absent, matching the SUT's behaviour.
    private static string ExpectedFullPath()
    {
        using var machineEnv = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
        using var userEnv = Registry.CurrentUser.OpenSubKey(@"Environment");

        var machine = Environment.ExpandEnvironmentVariables(
            machineEnv?.GetValue("Path", "") as string ?? "");
        var user = Environment.ExpandEnvironmentVariables(
            userEnv?.GetValue("Path", "") as string ?? "");

        return string.IsNullOrEmpty(user) ? machine : $"{machine};{user}";
    }
}
