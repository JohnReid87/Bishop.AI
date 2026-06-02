using Bishop.App;
using Bishop.App.Scripts.LaunchScript;
using Bishop.App.Services.Terminal;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Scripts;

public sealed class LaunchScriptCommandHandlerTests
{
    private static readonly string ScriptsRoot = BishopScriptsFolderPath.Resolve();
    private static string ScriptPath(string filename) => Path.Combine(ScriptsRoot, filename);

    [Fact]
    public async Task Handle_ReturnsTrue_WhenLauncherSucceeds()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1")),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(false);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1")),
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoArgs_LaunchesWithFileFlag()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);
        var scriptPath = ScriptPath("deploy.ps1");

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(scriptPath),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            ScriptsRoot,
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 2 && a[0] == "-File" && a[1] == scriptPath),
            null);
    }

    [Fact]
    public async Task Handle_WithArgs_AppendsArgTokensAfterFileFlag()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);
        var scriptPath = ScriptPath("deploy.ps1");

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(scriptPath, "--env prod"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            ScriptsRoot,
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 4
                && a[0] == "-File"
                && a[1] == scriptPath
                && a[2] == "--env"
                && a[3] == "prod"),
            null);
    }

    [Fact]
    public async Task Handle_WhitespaceOnlyArgs_TreatedAsNoArgs()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1"), "   "),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(),
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 2),
            null);
    }

    [Fact]
    public async Task Handle_QuotedPathWithSpaces_PassedAsSingleToken()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1"), @"-InputFile ""C:\My Docs\data.csv"""),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(),
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 4
                && a[2] == "-InputFile"
                && a[3] == @"C:\My Docs\data.csv"),
            null);
    }

    [Fact]
    public async Task Handle_QuotedStringWithSpaces_PassedAsSingleToken()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1"), "-Message \"hello world\""),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(),
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 4
                && a[2] == "-Message"
                && a[3] == "hello world"),
            null);
    }

    [Fact]
    public async Task Handle_UsesScriptDirectoryAsWorkingDirectory()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);
        var scriptPath = Path.Combine(ScriptsRoot, "sub", "script.ps1");
        var expectedDir = Path.Combine(ScriptsRoot, "sub");

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(scriptPath),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            expectedDir,
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Any<TerminalSnap?>());
    }

    [Fact]
    public async Task Handle_SingleQuotedArg_PassedAsSingleToken()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1"), "-Message 'hello world'"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(),
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 4
                && a[2] == "-Message"
                && a[3] == "hello world"),
            null);
    }

    // ── CWE-78: SplitArgs metachar sanitization ──────────────────────────────
    // Typing `; & calc.exe` or similar into the Scripts args field must not inject shell commands.
    // The bare `&` token becomes empty after Sanitize and is dropped; calc.exe survives only as
    // a harmless positional arg to pwsh.exe, not as a cmd.exe command separator.

    [Theory]
    [InlineData("; & calc.exe", new[] { ";", "calc.exe" })]
    [InlineData("--flag | badcmd", new[] { "--flag", "badcmd" })]
    [InlineData("value >output", new[] { "value", "output" })]
    [InlineData("x^y", new[] { "xy" })]
    public async Task Handle_ArgsWithShellMetachars_MetacharsStrippedBeforePassingToLauncher(
        string rawArgs, string[] expectedExtraTokens)
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        await handler.Handle(
            new LaunchScriptCommand(ScriptPath("deploy.ps1"), rawArgs),
            CancellationToken.None);

        // Assert — tokens reaching the launcher must not contain cmd.exe metacharacters
        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string[]>(a => a.Length == expectedExtraTokens.Length + 2
                && a[0] == "-File"
                && a.Skip(2).SequenceEqual(expectedExtraTokens)),
            Arg.Any<TerminalSnap?>());
    }

    // ── CWE-22: path containment guard ───────────────────────────────────────

    [Fact]
    public async Task Handle_ScriptPathOutsideScriptsFolder_ThrowsArgumentException()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        var act = async () => await handler.Handle(
            new LaunchScriptCommand(@"C:\Windows\System32\evil.ps1"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        launcher.DidNotReceive().LaunchCommand(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>());
    }

    [Fact]
    public async Task Handle_PathTraversalIntoScriptsParent_ThrowsArgumentException()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchScriptCommandHandler(launcher);
        // Traversal resolves to a sibling of Bishop.AI\scripts (e.g. Bishop.AI\evil.ps1)
        var traversalPath = Path.Combine(ScriptsRoot, "..", "evil.ps1");

        // Act
        var act = async () => await handler.Handle(
            new LaunchScriptCommand(traversalPath),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        launcher.DidNotReceive().LaunchCommand(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>());
    }
}
