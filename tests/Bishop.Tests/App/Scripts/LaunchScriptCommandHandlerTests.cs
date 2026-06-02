using Bishop.App.Scripts.LaunchScript;
using Bishop.App.Services.Terminal;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Scripts;

public sealed class LaunchScriptCommandHandlerTests
{
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
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1"),
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
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1"),
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

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\scripts",
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 2 && a[0] == "-File" && a[1] == @"C:\scripts\deploy.ps1"),
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

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1", "--env prod"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\scripts",
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 4
                && a[0] == "-File"
                && a[1] == @"C:\scripts\deploy.ps1"
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
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1", "   "),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\scripts",
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
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1", @"-InputFile ""C:\My Docs\data.csv"""),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\scripts",
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
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1", "-Message \"hello world\""),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\scripts",
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

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(@"C:\foo\bar\script.ps1"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\foo\bar",
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
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1", "-Message 'hello world'"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\scripts",
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
    [InlineData("; & calc.exe", new[] { "-File", @"C:\scripts\deploy.ps1", ";", "calc.exe" })]
    [InlineData("--flag | badcmd", new[] { "-File", @"C:\scripts\deploy.ps1", "--flag", "badcmd" })]
    [InlineData("value >output", new[] { "-File", @"C:\scripts\deploy.ps1", "value", "output" })]
    [InlineData("x^y", new[] { "-File", @"C:\scripts\deploy.ps1", "xy" })]
    public async Task Handle_ArgsWithShellMetachars_MetacharsStrippedBeforePassingToLauncher(
        string rawArgs, string[] expectedTokens)
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);

        // Act
        await handler.Handle(
            new LaunchScriptCommand(@"C:\scripts\deploy.ps1", rawArgs),
            CancellationToken.None);

        // Assert — tokens reaching the launcher must not contain cmd.exe metacharacters
        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string[]>(a => a.SequenceEqual(expectedTokens)),
            Arg.Any<TerminalSnap?>());
    }

    [Fact]
    public async Task Handle_ScriptAtRootPath_UsesUserProfileAsWorkingDirectory()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchScriptCommandHandler(launcher);
        // Path.GetDirectoryName(@"C:\") returns null on Windows, triggering the ?? fallback
        var expectedDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Act
        var result = await handler.Handle(
            new LaunchScriptCommand(@"C:\"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            expectedDir,
            "pwsh.exe",
            Arg.Is<string[]>(a => a.Length == 2 && a[0] == "-File"),
            null);
    }
}
