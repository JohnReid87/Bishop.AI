using Bishop.App.Git;
using MediatR;

namespace Bishop.App.Batches.DeleteBatchBranch;

public sealed class DeleteBatchBranchCommandHandler
    : IRequestHandler<DeleteBatchBranchCommand, DeleteBatchBranchResult>
{
    private readonly IGitCli _git;

    public DeleteBatchBranchCommandHandler(IGitCli git) => _git = git;

    public async Task<DeleteBatchBranchResult> Handle(
        DeleteBatchBranchCommand request, CancellationToken cancellationToken)
    {
        var checkedOut = await _git.GetWorktreeBranchesAsync(request.WorkspacePath, cancellationToken);
        if (checkedOut.Contains(request.BranchName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Branch '{request.BranchName}' is currently checked out in a worktree and cannot be deleted.");

        await _git.DeleteLocalBranchAsync(request.WorkspacePath, request.BranchName, cancellationToken);
        return new DeleteBatchBranchResult();
    }
}
