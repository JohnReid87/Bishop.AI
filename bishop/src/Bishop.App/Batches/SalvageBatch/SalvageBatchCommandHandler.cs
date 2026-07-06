using Bishop.App.Batches.CleanUpBatch;
using Bishop.App.Batches.MergeBatch;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.SalvageBatch;

internal sealed class SalvageBatchCommandHandler : IRequestHandler<SalvageBatchCommand, SalvageBatchResult>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;
    private readonly IGitCli _git;

    public SalvageBatchCommandHandler(
        IDbContextFactory<BishopDbContext> dbFactory,
        ISender sender,
        IGitCli git)
    {
        _dbFactory = dbFactory;
        _sender = sender;
        _git = git;
    }

    public async Task<SalvageBatchResult> Handle(SalvageBatchCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var matches = await db.Batches.ByName(request.Name).ToListAsync(cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        // Salvage recovers a partially-succeeded run, which only a Working batch can be.
        if (batch.Status != BatchStatus.Working)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' must be Working to salvage; current status is {batch.Status}.");

        // Never salvage under a live run — merging and resetting beneath it would corrupt that run.
        if (BatchLock.TryReadOwnerPid(batch.WorktreePath, batch.Id, out var pid) && BatchLock.IsProcessAlive(pid))
            return new SalvageBatchResult(SalvageBatchOutcome.LockAlive, LockOwnerPid: pid);

        // Split the batch: Done cards rode the branch and will merge; the rest are unfinished and get ejected.
        var batchCards = await db.Cards
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .Select(c => new { c.Number, c.LaneName })
            .ToListAsync(cancellationToken);

        var mergedNumbers = batchCards.Where(c => c.LaneName == SystemLaneNames.Done).Select(c => c.Number).ToList();
        var ejectedNumbers = batchCards.Where(c => c.LaneName != SystemLaneNames.Done).Select(c => c.Number).ToList();

        // Nothing succeeded — there is no prefix to deliver. Abandon is the right verb here, not salvage.
        if (mergedNumbers.Count == 0)
            return new SalvageBatchResult(SalvageBatchOutcome.NothingSucceeded, EjectedCardNumbers: ejectedNumbers);

        if (!request.Confirm)
            return new SalvageBatchResult(
                SalvageBatchOutcome.NeedsConfirmation,
                MergedCardNumbers: mergedNumbers,
                EjectedCardNumbers: ejectedNumbers);

        // Merge first so a conflict aborts cleanly (git merge --abort) before any DB or worktree change is made.
        var merge = await _sender.Send(new MergeBatchCommand(request.Name, request.WorkspacePath), cancellationToken);
        if (!merge.Success)
            return new SalvageBatchResult(
                SalvageBatchOutcome.MergeConflict,
                MergedCardNumbers: mergedNumbers,
                EjectedCardNumbers: ejectedNumbers,
                ConflictFiles: merge.ConflictFiles,
                ErrorMessage: merge.ErrorMessage);

        // Discard the failed card's uncommitted work and drop the stale lock so clean-up can remove the worktree.
        var status = await _git.GetWorkingTreeStatusAsync(batch.WorktreePath, cancellationToken);
        if (status is GetWorkingTreeStatusResult.Dirty)
        {
            await _git.ResetHardAsync(batch.WorktreePath, cancellationToken);
            await _git.CleanWorkingTreeAsync(batch.WorktreePath, cancellationToken);
        }
        BatchLock.DeleteLockFile(BatchLock.LockFilePath(batch.WorktreePath, batch.Id));

        // Eject unfinished cards: back to To Do, batchless, so a failing card can be re-specced on its own.
        await db.Cards
            .Where(c => c.BatchId == batch.Id && c.LaneName != SystemLaneNames.Done)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LaneName, SystemLaneNames.ToDo)
                .SetProperty(c => c.BatchId, (Guid?)null),
                cancellationToken);

        // Finalize the succeeded prefix: remove worktree, delete branch, close the batch, close Done cards.
        var cleanUp = await _sender.Send(new CleanUpBatchCommand(request.Name, request.WorkspacePath), cancellationToken);

        return new SalvageBatchResult(
            SalvageBatchOutcome.Salvaged,
            MergedCardNumbers: mergedNumbers,
            EjectedCardNumbers: ejectedNumbers,
            ClosedCardNumbers: cleanUp.ClosedCardNumbers);
    }
}
