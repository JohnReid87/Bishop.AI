using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Services.Terminal;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.Skills;

public sealed class LaunchSkillCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTrue_WhenLauncherSucceeds()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(true);
        var handler = new LaunchSkillCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

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
        var handler = new LaunchSkillCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

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
        var handler = new LaunchSkillCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

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
        var handler = new LaunchSkillCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        launcher.Received(1).Launch(@"C:\workspace", "code .", null, null);
    }

    [Fact]
    public async Task Handle_CallsSeedAsync_WithWorkspacePath()
    {
        // Arrange
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchSkillCommandHandler(launcher, seeder);
        var ct = new CancellationToken();

        // Act
        await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), ct);

        // Assert
        await seeder.Received(1).SeedAsync(@"C:\workspace", ct);
    }

    [Fact]
    public async Task Handle_WhenSeedAsyncThrows_PropagatesExceptionAndDoesNotLaunch()
    {
        // Arrange
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        seeder.SeedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("seed failed")));
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchSkillCommandHandler(launcher, seeder);

        // Act
        var act = async () => await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("seed failed");
        launcher.DidNotReceive().Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_WhenLaunchThrows_PropagatesException()
    {
        // Arrange
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>())
            .Throws(new InvalidOperationException("launch failed"));
        var handler = new LaunchSkillCommandHandler(launcher, seeder);

        // Act
        var act = async () => await handler.Handle(new LaunchSkillCommand(@"C:\workspace", "code ."), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("launch failed");
    }
}
