#pragma warning disable CA1416 // Windows-only; tests run on Windows
using Bishop.App.Services.Terminal;
using FluentAssertions;
using System.Diagnostics;
using System.Reflection;

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

    // ── BuildFullPath ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildFullPath_BothRegistryKeysMissing_ReturnsEmptyString()
    {
        // Both OpenSubKey() calls returning null triggers the ?? "" null-coalescing on both
        // paths; ExpandEnvironmentVariables("") yields "" and CombinePaths("", "") yields "".
        var result = TerminalLauncher.BuildFullPath(null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildFullPath_MachinePathPresent_UserPathMissing_ReturnsMachinePath()
    {
        var result = TerminalLauncher.BuildFullPath(@"C:\Windows\System32", null);

        result.Should().Be(@"C:\Windows\System32");
    }

    [Fact]
    public void BuildFullPath_BothPathsPresent_CombinesWithSemicolon()
    {
        var result = TerminalLauncher.BuildFullPath(@"C:\Windows\System32", @"C:\Users\user\bin");

        result.Should().Be(@"C:\Windows\System32;C:\Users\user\bin");
    }

    [Fact]
    public void CombinePaths_UserPathEmpty_ReturnsMachinePathOnly()
    {
        var method = typeof(TerminalLauncher)
            .GetMethod("CombinePaths", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, [@"C:\Windows\System32", ""])!;

        result.Should().Be(@"C:\Windows\System32");
    }

    [Fact]
    public void CombinePaths_BothPathsEmpty_ReturnsEmptyString()
    {
        var method = typeof(TerminalLauncher)
            .GetMethod("CombinePaths", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, ["", ""])!;

        result.Should().BeEmpty();
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
    public void Launch_WithClaudeArgs_AppendsClaudeArgsToArgumentList()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", "code .", null);

        // Assert
        _started.Single().ArgumentList.Should().Contain("code .");
    }

    [Fact]
    public void Launch_WithModelId_AppendsModelFlagToArgumentList()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null, "claude-opus-4");

        // Assert
        _started.Single().ArgumentList.Should().ContainInOrder("--model", "claude-opus-4");
    }

    [Fact]
    public void Launch_WithModelIdAndClaudeArgs_AppendsBoth()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", "code .", null, "claude-opus-4");

        // Assert
        var list = _started.Single().ArgumentList;
        list.Should().ContainInOrder("--model", "claude-opus-4");
        list.Should().Contain("code .");
    }

    [Fact]
    public void Launch_WithClaudeArgsSnapAndModelId_AllPropagateToProcessStartInfo()
    {
        // Verifies that a snap value does not suppress claudeArgs or modelId in the ProcessStartInfo.
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        sut.Launch(@"C:\Repo", "code .", snap, "claude-opus-4");

        var list = _started.Single().ArgumentList;
        list.Should().ContainInOrder("--model", "claude-opus-4");
        list.Should().Contain("code .");
    }

    [Fact]
    public void Launch_WithoutModelId_NoModelFlag()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert
        _started.Single().ArgumentList.Should().NotContain("--model");
    }

    [Fact]
    public void Launch_SetsPathToBuildFullPathResult()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.Launch(@"C:\Repo", null, null);

        // Assert — PATH must be the merged registry PATH; never empty, always multi-entry.
        _started.Single().Environment.Should().ContainKey("PATH");
        var launchPath = _started.Single().Environment["PATH"];
        launchPath.Should().NotBeNullOrEmpty();
        launchPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);
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
        _started.Single().ArgumentList.Should().Contain("pwsh.exe");
    }

    [Fact]
    public void LaunchPlain_WtFound_PwshUnavailable_UsesPowerShell()
    {
        // Arrange
        var sut = CreateSut(wtExists: true, pwshExists: false);

        // Act
        sut.LaunchPlain(@"C:\Repo", null);

        // Assert
        _started.Single().ArgumentList.Should().Contain("powershell.exe");
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

        // Assert — PATH must be the merged registry PATH; never empty, always multi-entry.
        _started.Single().Environment.Should().ContainKey("PATH");
        var launchPlainPath = _started.Single().Environment["PATH"];
        launchPlainPath.Should().NotBeNullOrEmpty();
        launchPlainPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);
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
        var result = sut.LaunchCommand(@"C:\Repo", "bishop", ["batch", "run"], null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchCommand_WtFound_BuildsArgumentListWithCommandAndArgs()
    {
        // Arrange
        var sut = CreateSut(wtExists: true);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", ["batch", "run", "--tag", "test", "--max", "5"], null);

        // Assert
        _started.Single().ArgumentList.Should().Equal(
            "-d", @"C:\Repo", "cmd.exe", "/k", "bishop", "batch", "run", "--tag", "test", "--max", "5");
    }

    [Fact]
    public void LaunchCommand_WtNotFound_FallsBackToPowerShellWithCommand()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", ["batch", "run", "--max", "10"], null);

        // Assert
        _started.Single().FileName.Should().Be("powershell.exe");
        _started.Single().ArgumentList.Should().Equal("-NoExit", "-Command", "bishop", "batch", "run", "--max", "10");
        _started.Single().WorkingDirectory.Should().Be(@"C:\Repo");
    }

    [Fact]
    public void LaunchCommand_WithNoArgs_OnlyCommandInArgumentList()
    {
        var sut = CreateSut(wtExists: false);

        sut.LaunchCommand(@"C:\Repo", "bishop", [], null);

        _started.Single().ArgumentList.Should().Equal("-NoExit", "-Command", "bishop");
    }

    [Fact]
    public void LaunchCommand_SetsPathToMergedRegistryPath()
    {
        // Arrange
        var sut = CreateSut(wtExists: false);

        // Act
        sut.LaunchCommand(@"C:\Repo", "bishop", ["batch", "run"], null);

        // Assert — PATH must be the merged registry PATH; never empty, always multi-entry.
        var path = _started.Single().Environment["PATH"];
        path.Should().NotBeNullOrEmpty();
        path.Split(';', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void LaunchCommand_WithSnap_WtFound_ReturnsTrue()
    {
        var sut = CreateSut(wtExists: true);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.LaunchCommand(@"C:\Repo", "bishop", ["batch", "run"], snap);

        result.Should().BeTrue();
    }

    [Fact]
    public void LaunchCommand_WithSnap_WtNotFound_ReturnsFalse()
    {
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.LaunchCommand(@"C:\Repo", "bishop", ["batch", "run"], snap);

        result.Should().BeFalse();
    }

    [Fact]
    public void LaunchCommand_WorkspacePathWithSpaceAndQuote_PassedLiterallyInArgumentList()
    {
        // WT path: ArgumentList = ["-d", <workingDirectory>, "cmd.exe", "/k", ...]
        var sut = CreateSut(wtExists: true);
        var path = @"C:\My ""Repo"" Path";

        sut.LaunchCommand(path, "bishop", ["batch", "run"], null);

        _started.Single().ArgumentList[1].Should().Be(path);
    }

    [Fact]
    public void LaunchCommand_ArgWithAmpersand_PassedLiterallyInArgumentList()
    {
        var sut = CreateSut(wtExists: false);

        sut.LaunchCommand(@"C:\Repo", "bishop", ["--tag", "tag&bad"], null);

        _started.Single().ArgumentList.Should().Contain("tag&bad");
    }

    [Fact]
    public void LaunchCommand_ArgWithSpace_PassedLiterallyInArgumentList()
    {
        var sut = CreateSut(wtExists: false);

        sut.LaunchCommand(@"C:\Repo", "bishop", ["--tag", "my tag"], null);

        _started.Single().ArgumentList.Should().Contain("my tag");
    }

    // ── ArgumentException catch branches ─────────────────────────────────────

    [Fact]
    public void Launch_PathSegmentWithIllegalCharsIsSkipped_FallsBackToPowerShell()
    {
        // Injects a PATH segment containing '<', a genuinely illegal Windows path character,
        // via the environment variable so the malformed entry originates from PATH itself.
        // _fileExists throws ArgumentException when given a path built from that segment,
        // reflecting what File.Exists does with such paths on Windows.
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", @"C:\Dir<Invalid");
            var alias = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "wt.exe");
            var sut = new TerminalLauncher(
                path =>
                {
                    if (path == alias) return false;
                    if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        throw new ArgumentException($"Illegal characters in path: {path}");
                    return false;
                },
                psi => _started.Add(psi));

            sut.Launch(@"C:\Repo", null, null);

            _started.Single().FileName.Should().Be("powershell.exe");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void Launch_FileExistsThrowsArgumentExceptionForWt_SkipsSegmentAndFallsBackToPowerShell()
    {
        // Exercises the catch(ArgumentException) in FindWindowsTerminal's PATH loop directly,
        // without manipulating the PATH environment variable — analogous to the HasPwsh test below.
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

    [Fact]
    public void HasPwsh_EmptyAndWhitespacePathSegments_DoesNotThrowAndReturnsFalse()
    {
        // Drives HasPwsh via reflection with a fullPath string containing empty and
        // whitespace-only segments (";;  ;"). Each trims to "" and produces the relative
        // candidate "pwsh.exe" via Path.Combine("", "pwsh.exe") — no ArgumentException.
        var sut = new TerminalLauncher(_ => false, psi => _started.Add(psi));
        var hasPwsh = typeof(TerminalLauncher)
            .GetMethod("HasPwsh", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var result = (bool)hasPwsh.Invoke(sut, [";;  ;C:\\NonExistentDir"])!;

        result.Should().BeFalse();
    }

    // ── Null / empty workingDirectory and claudeArgs ──────────────────────────

    [Fact]
    public void Launch_NullWorkingDirectory_SetsEmptyWorkingDirectory()
    {
        var sut = CreateSut(wtExists: false);
        sut.Launch(null!, null, null);
        _started.Single().WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public void Launch_EmptyWorkingDirectory_SetsEmptyWorkingDirectory()
    {
        var sut = CreateSut(wtExists: false);
        sut.Launch(string.Empty, null, null);
        _started.Single().WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public void Launch_EmptyClaudeArgs_AppendsEmptyStringToArgumentList()
    {
        var sut = CreateSut(wtExists: false);
        sut.Launch(@"C:\Repo", "", null);

        var psi = _started.Single();
        psi.ArgumentList.Should().Equal("-NoExit", "-Command", "claude", "");
        psi.ArgumentList.Should().NotContain("--model");
        psi.WorkingDirectory.Should().Be(@"C:\Repo");
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

    // ── UseShellExecute ───────────────────────────────────────────────────────

    [Fact]
    public void Launch_SetsUseShellExecuteToFalse()
    {
        // UseShellExecute = false is a security-relevant property set on every PSI.
        // This assertion ensures a refactor cannot silently flip it.
        var sut = CreateSut(wtExists: false);
        sut.Launch(@"C:\Repo", null, null);
        _started.Single().UseShellExecute.Should().BeFalse();
    }

    // ── Parameterless constructor ─────────────────────────────────────────────

    [Fact]
    public void ParameterlessConstructor_DoesNotThrow()
    {
        // Verifies the default File.Exists / Process.Start wiring compiles and initialises.
        var act = () => _ = new TerminalLauncher();
        act.Should().NotThrow();
    }

    [Fact]
    public void ParameterlessConstructor_DefaultFileExistsDelegateReturnsTrueForExistingFile()
    {
        var sut = new TerminalLauncher();
        var fileExists = (Func<string, bool>)typeof(TerminalLauncher)
            .GetField("_fileExists", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sut)!;

        var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        fileExists(cmdPath).Should().BeTrue();
    }

    // ── SnapLater / ApplySnap ─────────────────────────────────────────────────
    // ApplySnap and the inner window-poll loop in SnapLater depend on real win32
    // window handles (EnumWindows, DwmGetWindowAttribute, SetWindowPos). They are
    // deliberately untested here: no real windows exist in a unit-test process, so
    // the background Task launched by SnapLater will time out harmlessly after 3 s
    // and return without snapping. The smoke tests below confirm the snap code path
    // is reached without crashing the test host.

    [Fact]
    public void LaunchPlain_WithSnap_WtNotFound_StartsProcessAndReturnsFalse()
    {
        var sut = CreateSut(wtExists: false);
        var snap = new TerminalSnap(0, 0, 1280, 1440);

        var result = sut.LaunchPlain(@"C:\Repo", snap);

        result.Should().BeFalse();
        _started.Single().FileName.Should().Be("powershell.exe");
    }

}
