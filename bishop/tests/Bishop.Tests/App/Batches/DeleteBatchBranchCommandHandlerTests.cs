using Bishop.App.Batches.DeleteBatchBranch;
using Bishop.App.Git;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Batches;

public sealed class DeleteBatchBranchCommandHandlerTests
{
    private const string WorkspacePath = @"C:\fake-workspace";
    private const string BranchName = "bishop/my-batch";

    private static DeleteBatchBranchCommandHandler CreateHandler(IGitCli git)
        => new(git);

    [Fact]
    public async Task DeletesBranch_WhenNotCheckedOut()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([]);

        await CreateHandler(git).Handle(new DeleteBatchBranchCommand(WorkspacePath, BranchName), default);

        await git.Received(1).DeleteLocalBranchAsync(WorkspacePath, BranchName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_WhenBranchIsCheckedOut()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>()).Returns([BranchName]);

        Func<Task> act = () => CreateHandler(git).Handle(
            new DeleteBatchBranchCommand(WorkspacePath, BranchName), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{BranchName}*");
        await git.DidNotReceive().DeleteLocalBranchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckedOutComparison_IsCaseInsensitive()
    {
        var git = Substitute.For<IGitCli>();
        git.GetWorktreeBranchesAsync(WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(["Bishop/My-Batch"]);

        Func<Task> act = () => CreateHandler(git).Handle(
            new DeleteBatchBranchCommand(WorkspacePath, BranchName), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
