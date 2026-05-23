using Bishop.App.Git;
using Bishop.App.Git.Push;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Git;

public sealed class PushCommandHandlerTests
{
    private const string WorkspacePath = @"C:\repos\my-project";

    private static PushCommandHandler CreateSut(IGitCli git) => new(git);

    [Fact]
    public async Task Handle_PassesWorkspacePathToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult(true, null));
        var sut = CreateSut(git);

        // Act
        await sut.Handle(new PushCommand(WorkspacePath), CancellationToken.None);

        // Assert
        await git.Received(1).PushAsync(WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForwardsCancellationTokenToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult(true, null));
        var sut = CreateSut(git);
        using var cts = new CancellationTokenSource();

        // Act
        await sut.Handle(new PushCommand(WorkspacePath), cts.Token);

        // Assert
        await git.Received(1).PushAsync(Arg.Any<string>(), cts.Token);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessResult()
    {
        // Arrange
        var expected = new PushResult(true, "To origin\n   abc..def  HEAD -> main");
        var git = Substitute.For<IGitCli>();
        git.PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new PushCommand(WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsFailureResult()
    {
        // Arrange
        var expected = new PushResult(false, "fatal: 'origin' does not appear to be a git repository");
        var git = Substitute.For<IGitCli>();
        git.PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new PushCommand(WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }
}
