using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Terminal;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Skills;

public sealed class LaunchSkillCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTrue_WhenLauncherSucceeds()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(true);
        var handler = new LaunchSkillCommandHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(false);
        var handler = new LaunchSkillCommandHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForwardsAllArgumentsToLauncher()
    {
        // Arrange
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchSkillCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "/bish-work-on-card 42", snap, "claude-opus-4-7"), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "/bish-work-on-card 42", snap, "claude-opus-4-7");
    }

    [Fact]
    public async Task Handle_ForwardsNullSnapAndModelId_WhenNotProvided()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchSkillCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "code .", null, null);
    }
}
