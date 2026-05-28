using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bishop.App.Batches.CleanUpBatch;

public sealed class CleanUpBatchCommandHandler : IRequestHandler<CleanUpBatchCommand>
{
    private readonly IBatchRepository _batches;
    private readonly IGitCli _git;
    private readonly ILogger<CleanUpBatchCommandHandler> _logger;

    public CleanUpBatchCommandHandler(IBatchRepository batches, IGitCli git, ILogger<CleanUpBatchCommandHandler> logger)
    {
        _batches = batches;
        _git = git;
        _logger = logger;
    }

    public async Task Handle(CleanUpBatchCommand request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        var isMerged = await _git.IsBranchMergedIntoAsync(
            request.WorkspacePath, batch.BranchName, batch.BaseBranch, cancellationToken);
        if (!isMerged)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' has not been merged yet — run 'bishop batch merge {request.Name}' first.");

        // Best-effort worktree removal
        if (!string.IsNullOrEmpty(batch.WorktreePath))
        {
            try
            {
                await _git.RemoveWorktreeAsync(request.WorkspacePath, batch.WorktreePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove worktree '{Path}' for batch '{Name}'.",
                    batch.WorktreePath, batch.Name);
            }
        }

        // Delete local branch if present and not checked out elsewhere
        var branchExists = await _git.LocalBranchExistsAsync(request.WorkspacePath, batch.BranchName, cancellationToken);
        if (branchExists)
        {
            var checkedOut = await _git.GetWorktreeBranchesAsync(request.WorkspacePath, cancellationToken);
            if (!checkedOut.Contains(batch.BranchName, StringComparer.OrdinalIgnoreCase))
                await _git.DeleteLocalBranchAsync(request.WorkspacePath, batch.BranchName, cancellationToken);
        }

        if (batch.Status != BatchStatus.Closed)
            await _batches.CloseAsync(batch.Id, BatchClosedReason.Finished, cancellationToken);
    }
}
