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
        launcher.Received(1).LaunchCommand(@"C:\workspace", "bishop", "work-next --tag test --max 5", null);
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
        launcher.Received(1).LaunchCommand(@"C:\workspace", "bishop", "work-next --max 10", null);
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
        launcher.Received(1).LaunchCommand(@"C:\workspace", "bishop", "work-next --max 10", null);
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
        launcher.Received(1).LaunchCommand(@"C:\workspace", "bishop", "work-next --tag test --max 0", null);
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
        launcher.Received(1).LaunchCommand(@"C:\workspace", "bishop", "work-next --max 10", snap);
    }

    [Fact]
    public async Task Handle_ReturnsLauncherResult()
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
}
