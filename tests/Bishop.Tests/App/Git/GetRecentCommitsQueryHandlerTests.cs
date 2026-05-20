using Bishop.App.Git;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Git;

public sealed class GetRecentCommitsQueryHandlerTests
{
    private const string WorkspacePath = @"C:\repos\my-project";

    private static GetRecentCommitsQueryHandler CreateSut(IGitCli git) => new(git);

    [Fact]
    public async Task Handle_PassesWorkspacePathToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.NoCommits());
        var sut = CreateSut(git);

        // Act
        await sut.Handle(new GetRecentCommitsQuery(WorkspacePath), CancellationToken.None);

        // Assert
        await git.Received(1).GetRecentCommitsAsync(WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForwardsCancellationTokenToGitCli()
    {
        // Arrange
        var git = Substitute.For<IGitCli>();
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetRecentCommitsResult.NoCommits());
        var sut = CreateSut(git);
        using var cts = new CancellationTokenSource();

        // Act
        await sut.Handle(new GetRecentCommitsQuery(WorkspacePath), cts.Token);

        // Assert
        await git.Received(1).GetRecentCommitsAsync(Arg.Any<string>(), cts.Token);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessResult_WithCommits()
    {
        // Arrange
        var commits = new List<CommitInfo>
        {
            new("abc1234", "abc1234def5678901234567890", "Initial commit", "", DateTimeOffset.UtcNow)
        };
        var expected = new GetRecentCommitsResult.Success(commits);
        var git = Substitute.For<IGitCli>();
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetRecentCommitsQuery(WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsNotAGitRepoResult()
    {
        // Arrange
        var expected = new GetRecentCommitsResult.NotAGitRepo();
        var git = Substitute.For<IGitCli>();
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetRecentCommitsQuery(WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsGitNotFoundResult()
    {
        // Arrange
        var expected = new GetRecentCommitsResult.GitNotFound();
        var git = Substitute.For<IGitCli>();
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetRecentCommitsQuery(WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ReturnsNoCommitsResult()
    {
        // Arrange
        var expected = new GetRecentCommitsResult.NoCommits();
        var git = Substitute.For<IGitCli>();
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateSut(git);

        // Act
        var result = await sut.Handle(new GetRecentCommitsQuery(WorkspacePath), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }
}
