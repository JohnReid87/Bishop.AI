using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Batches.MergeBatch;

public sealed class MergeBatchCommandHandler : IRequestHandler<MergeBatchCommand, MergeBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IGitCli _git;

    public MergeBatchCommandHandler(IBatchRepository batches, IGitCli git)
    {
        _batches = batches;
        _git = git;
    }

    public async Task<MergeBatchResult> Handle(MergeBatchCommand request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];
        if (batch.Status != BatchStatus.Working)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' must be Working to merge; current status is {batch.Status}.");

        var currentBranch = await _git.GetCurrentBranchAsync(request.WorkspacePath, cancellationToken);
        if (currentBranch != batch.BaseBranch)
            return new MergeBatchResult(false, [],
                $"Cannot merge — HEAD is on '{currentBranch}', expected '{batch.BaseBranch}'. Check out '{batch.BaseBranch}' first.");

        var mergeResult = await _git.MergeAsync(request.WorkspacePath, batch.BranchName, cancellationToken);
        return new MergeBatchResult(mergeResult.Success, mergeResult.ConflictFiles, mergeResult.ErrorMessage);
    }
}
