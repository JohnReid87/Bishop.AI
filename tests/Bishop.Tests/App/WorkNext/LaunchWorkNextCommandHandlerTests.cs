using Bishop.App.Services.Terminal;
using Bishop.App.WorkNext.LaunchWorkNext;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bishop.Tests.App.WorkNext;

public sealed class LaunchWorkNextCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithTag_RendersTagFlag()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "test", 5), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            "work-next --tag test --max 5 --model claude-sonnet-4-6",
            null);
    }

    [Fact]
    public async Task Handle_WithNullTag_OmitsTagFlag()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            "work-next --max 10 --model claude-sonnet-4-6",
            null);
    }

    [Fact]
    public async Task Handle_WithEmptyTag_OmitsTagFlag()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "", 10), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            "work-next --max 10 --model claude-sonnet-4-6",
            null);
    }

    [Fact]
    public async Task Handle_WithMaxZero_RendersUncapped()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "test", 0), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            "work-next --tag test --max 0 --model claude-sonnet-4-6",
            null);
    }

    [Fact]
    public async Task Handle_ForwardsSnap()
    {
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10, snap), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            "work-next --max 10 --model claude-sonnet-4-6",
            snap);
    }

    [Fact]
    public async Task Handle_ReturnsLauncherResult_WhenTrue()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>()).Returns(true);
        var handler = new LaunchWorkNextCommandHandler(launcher);

        var result = await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsLauncherResult_WhenFalse()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>()).Returns(false);
        var handler = new LaunchWorkNextCommandHandler(launcher);

        var result = await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithWhitespaceOnlyTag_TreatsAsTag()
    {
        // string.IsNullOrEmpty does not trim; whitespace is forwarded verbatim as the tag value
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", "  ", 10), default);

        var expectedArgs = string.Join(' ', "work-next", "--tag", "  ", "--max", "10", "--model", "claude-sonnet-4-6");
        launcher.Received(1).LaunchCommand(@"C:\workspace", "bishop", expectedArgs, null);
    }

    [Fact]
    public async Task Handle_IncludesModelFlag()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10, null, "claude-opus-4-7"), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            "work-next --max 10 --model claude-opus-4-7",
            null);
    }

    [Fact]
    public async Task Handle_DefaultsToSonnet_WhenModelNotSpecified()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkNextCommandHandler(launcher);

        await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 10), default);

        launcher.Received(1).LaunchCommand(
            @"C:\workspace",
            "bishop",
            $"work-next --max 10 --model {LaunchWorkNextCommand.DefaultModel}",
            null);
    }

    [Fact]
    public async Task Handle_PropagatesExceptionFromLauncher()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>())
            .Throws(new InvalidOperationException("launcher failed"));
        var handler = new LaunchWorkNextCommandHandler(launcher);

        var act = () => handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 5), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("launcher failed");
    }

    [Fact]
    public async Task Handle_DoesNotObserveCancellationToken()
    {
        // The handler delegates directly to the synchronous ITerminalLauncher call without
        // inspecting the token; this test documents that the ignore is deliberate.
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var handler = new LaunchWorkNextCommandHandler(launcher);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await handler.Handle(new LaunchWorkNextCommand(@"C:\workspace", null, 5), cts.Token);

        result.Should().BeTrue();
    }

    [Fact]
    public void BuildArgs_NullTagAndNullModel_ProducesMinimalArgs()
    {
        var result = LaunchWorkNextCommandHandler.BuildArgs(null, 5, null);

        result.Should().Be("work-next --max 5");
    }

    [Fact]
    public void BuildArgs_NullModel_OmitsModelFlag()
    {
        var result = LaunchWorkNextCommandHandler.BuildArgs("bug", 5, null);

        result.Should().Be("work-next --tag bug --max 5");
    }

    [Fact]
    public void BuildArgs_NullTag_OmitsTagFlag()
    {
        var result = LaunchWorkNextCommandHandler.BuildArgs(null, 5, "claude-opus-4-7");

        result.Should().Be("work-next --max 5 --model claude-opus-4-7");
    }

    [Fact]
    public void BuildArgs_NegativeMax_PassesThroughAsIs()
    {
        var result = LaunchWorkNextCommandHandler.BuildArgs(null, -1, null);

        result.Should().Be("work-next --max -1");
    }

    [Fact]
    public void BuildArgs_AllPartsProvided_ReturnsFullArgs()
    {
        var result = LaunchWorkNextCommandHandler.BuildArgs("bug", 3, "claude-opus-4-7");

        result.Should().Be("work-next --tag bug --max 3 --model claude-opus-4-7");
    }
}
