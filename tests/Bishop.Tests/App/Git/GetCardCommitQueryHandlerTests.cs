using Bishop.App.Git;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Git;

public sealed class GetCardCommitQueryHandlerTests
{
    private const int CardNumber = 42;
    private const string WorkspacePath = @"C:\repos\my-project";

    private static GetCardCommitQueryHandler CreateSut(IGitCli git) => new(git);

    [Fact]
    public async Task Handle_PassesCardNumberToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        var sut = CreateSut(git);

        // Act
        await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        await git.Received(1).GetCardCommitAsync(CardNumber, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesWorkspacePathToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        var sut = CreateSut(git);

        // Act
        await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        await git.Received(1).GetCardCommitAsync(Arg.Any<int>(), WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForwardsCancellationTokenToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.NotFound());
        var sut = CreateSut(git);
        using var cts = new CancellationTokenSource();

        // Act
        await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), cts.Token);

        // Assert
        await git.Received(1).GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), cts.Token);
    }

    [Fact]
    public async Task Handle_ReturnsFoundResult()
    {
        // Arrange
        var commit = new CommitInfo("abc1234", "abc1234def5678901234567890", "feat: Add something", "", DateTimeOffset.UtcNow, false);
        var expected = new GetCardCommitResult.Found(commit);
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundResult()
    {
        // Arrange
        var expected = new GetCardCommitResult.NotFound();
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsNotAGitRepoResult()
    {
        // Arrange
        var expected = new GetCardCommitResult.NotAGitRepo();
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsGitNotFoundResult()
    {
        // Arrange
        var expected = new GetCardCommitResult.GitNotFound();
        var git = Substitute.For<IGitCli>();
        git.GetCardCommitAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetCardCommitQuery(CardNumber, WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }
}
