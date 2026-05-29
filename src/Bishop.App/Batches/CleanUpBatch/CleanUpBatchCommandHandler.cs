using Bishop.App.Cards.CloseCard;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bishop.App.Batches.CleanUpBatch;

public sealed class CleanUpBatchCommandHandler : IRequestHandler<CleanUpBatchCommand, CleanUpBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;
    private readonly IGitCli _git;
    private readonly ILogger<CleanUpBatchCommandHandler> _logger;

    public CleanUpBatchCommandHandler(
        IBatchRepository batches,
        IDbContextFactory<BishopDbContext> dbFactory,
        ISender sender,
        IGitCli git,
        ILogger<CleanUpBatchCommandHandler> logger)
    {
        _batches = batches;
        _dbFactory = dbFactory;
        _sender = sender;
        _git = git;
        _logger = logger;
    }

    public async Task<CleanUpBatchResult> Handle(CleanUpBatchCommand request, CancellationToken cancellationToken)
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

        // Close Done-lane cards assigned to this batch
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var doneCards = await db.Cards
            .Where(c => c.BatchId == batch.Id && c.LaneName == SystemLaneNames.Done && !c.IsClosed)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var closedNumbers = new List<int>(doneCards.Count);
        foreach (var card in doneCards)
        {
            await _sender.Send(new CloseCardCommand(card.Id), cancellationToken);
            closedNumbers.Add(card.Number);
        }

        return new CleanUpBatchResult(closedNumbers);
    }
}
