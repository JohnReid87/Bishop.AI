using Bishop.App.Terminal;
using Bishop.App.WorkNext.LaunchWorkNext;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.WorkNext;

public sealed class LaunchWorkNextCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithTag_RendersTagFlag()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "test", 5), default);

        // Assert
        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            Arg.Is<string?>(a => a != null && a.Contains("--tag test") && a.Contains("--max 5")),
            null);
    }

    [Fact]
    public async Task Handle_WithNullTag_OmitsTagFlag()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        // Assert
        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            Arg.Is<string?>(a => a != null && !a.Contains("--tag") && a.Contains("--max 10")),
            null);
    }

    [Fact]
    public async Task Handle_WithEmptyTag_OmitsTagFlag()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "", 10), default);

        // Assert
        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            Arg.Is<string?>(a => a != null && !a.Contains("--tag") && a.Contains("--max 10")),
            null);
    }

    [Fact]
    public async Task Handle_WithMaxZero_RendersUncapped()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "test", 0), default);

        // Assert
        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            Arg.Is<string?>(a => a != null && a.Contains("--tag test") && a.Contains("--max 0")),
            null);
    }

    [Fact]
    public async Task Handle_ForwardsSnap()
    {
        // Arrange
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10, snap), default);

        // Assert
        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            Arg.Is<string?>(a => a != null && a.Contains("--max 10")),
            snap);
    }

    [Fact]
    public async Task Handle_ReturnsLauncherResult_WhenTrue()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>()).Returns(true);
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsLauncherResult_WhenFalse()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>()).Returns(false);
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act
        var result = await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithWhitespaceOnlyTag_TreatsAsTag()
    {
        // Arrange
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        // Act — string.IsNullOrEmpty does not trim; whitespace is forwarded as a real tag value
        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "  ", 10), default);

        // Assert
        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            Arg.Is<string?>(a => a != null && a.Contains("--tag")),
            null);
    }
}
