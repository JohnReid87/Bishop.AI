using Bishop.App.Batches.LaunchBatchTerminal;
using Bishop.App.Services.Terminal;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class LaunchBatchTerminalCommandHandlerTests
{
    [Fact]
    public async Task Handle_NotResume_LaunchesBishopWithBatchRunArgs()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchCommand(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<TerminalSnap?>())
            .Returns(true);
        var sut = new LaunchBatchTerminalCommandHandler(launcher);

        var result = await sut.Handle(
            new LaunchBatchTerminalCommand(@"C:\repo", "my-batch", "claude-opus-4-7", Resume: false),
            CancellationToken.None);

        result.Should().BeTrue();
        launcher.Received(1).LaunchCommand(
            @"C:\repo",
            "bishop",
            Arg.Is<string[]>(a =>
                a.Length == 5
                && a[0] == "batch" && a[1] == "run" && a[2] == "my-batch"
                && a[3] == "--model" && a[4] == "claude-opus-4-7"),
            Arg.Any<TerminalSnap?>());
    }

    [Fact]
    public async Task Handle_Resume_IncludesResumeFlag()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var sut = new LaunchBatchTerminalCommandHandler(launcher);

        await sut.Handle(
            new LaunchBatchTerminalCommand(@"C:\repo", "my-batch", "claude-opus-4-7", Resume: true),
            CancellationToken.None);

        launcher.Received(1).LaunchCommand(
            @"C:\repo",
            "bishop",
            Arg.Is<string[]>(a =>
                a.Contains("--resume")
                && a.Contains("my-batch")
                && a.Contains("--model")
                && a.Contains("claude-opus-4-7")),
            Arg.Any<TerminalSnap?>());
    }

    [Fact]
    public async Task Handle_PropagatesSnap()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var sut = new LaunchBatchTerminalCommandHandler(launcher);
        var snap = new TerminalSnap();

        await sut.Handle(
            new LaunchBatchTerminalCommand(@"C:\repo", "b", "m", Resume: false, Snap: snap),
            CancellationToken.None);

        launcher.Received(1).LaunchCommand(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Is<TerminalSnap?>(s => s == snap));
    }
}
