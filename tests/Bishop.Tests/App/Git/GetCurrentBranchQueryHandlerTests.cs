using Bishop.App.Git;
using Bishop.App.Git.GetCurrentBranch;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Git;

public sealed class GetCurrentBranchQueryHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToGitCli()
    {
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync("C:/repo", Arg.Any<CancellationToken>()).Returns("feature/x");
        var sut = new GetCurrentBranchQueryHandler(git);

        var result = await sut.Handle(new GetCurrentBranchQuery("C:/repo"), CancellationToken.None);

        result.Should().Be("feature/x");
        await git.Received(1).GetCurrentBranchAsync("C:/repo", Arg.Any<CancellationToken>());
    }
}
