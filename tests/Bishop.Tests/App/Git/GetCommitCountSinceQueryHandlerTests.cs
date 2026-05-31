using Bishop.App.Git;
using Bishop.App.Git.GetCommitCountSince;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Git;

public sealed class GetCommitCountSinceQueryHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToGitCli()
    {
        var git = Substitute.For<IGitCli>();
        git.GetCommitCountSinceAsync("abc123", "C:/repo", Arg.Any<CancellationToken>()).Returns(7);
        var sut = new GetCommitCountSinceQueryHandler(git);

        var result = await sut.Handle(new GetCommitCountSinceQuery("abc123", "C:/repo"), CancellationToken.None);

        result.Should().Be(7);
        await git.Received(1).GetCommitCountSinceAsync("abc123", "C:/repo", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenGitCliReturnsNull()
    {
        var git = Substitute.For<IGitCli>();
        git.GetCommitCountSinceAsync("deadbeef", "C:/repo", Arg.Any<CancellationToken>()).Returns((int?)null);
        var sut = new GetCommitCountSinceQueryHandler(git);

        var result = await sut.Handle(new GetCommitCountSinceQuery("deadbeef", "C:/repo"), CancellationToken.None);

        result.Should().BeNull();
    }
}
