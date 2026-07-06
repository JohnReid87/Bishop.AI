using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.RescueBatch;

internal sealed class RescueBatchCommandHandler : IRequestHandler<RescueBatchCommand, RescueBatchResult>
{
    private readonly IGitCli _git;
    private readonly ISender _sender;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RescueBatchCommandHandler(
        IGitCli git,
        ISender sender,
        IDbContextFactory<BishopDbContext> dbFactory)
    {
        _git = git;
        _sender = sender;
        _dbFactory = dbFactory;
    }

    public async Task<RescueBatchResult> Handle(RescueBatchCommand request, CancellationToken cancellationToken)
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

        // Only a Working batch can be mid-run; anything else has no interrupted run to rescue.
        if (batch.Status != BatchStatus.Working)
            return new RescueBatchResult(RescueBatchOutcome.NotRunning);

        // Refuse to touch a batch whose run is still alive — clearing its lock or
        // resetting its worktree underneath a live process would corrupt that run.
        if (BatchLock.TryReadOwnerPid(batch.WorktreePath, batch.Id, out var pid) && BatchLock.IsProcessAlive(pid))
            return new RescueBatchResult(RescueBatchOutcome.LockAlive, LockOwnerPid: pid);

        var status = await _git.GetWorkingTreeStatusAsync(batch.WorktreePath, cancellationToken);
        IReadOnlyList<string> dirtyPaths = status is GetWorkingTreeStatusResult.Dirty dirty ? dirty.Paths : [];

        // Resetting the worktree discards uncommitted work, so require explicit
        // confirmation before doing anything destructive (or any other change).
        if (dirtyPaths.Count > 0 && !request.ConfirmReset)
            return new RescueBatchResult(RescueBatchOutcome.NeedsConfirmation, DirtyPaths: dirtyPaths);

        var worktreeReset = false;
        if (dirtyPaths.Count > 0)
        {
            await _git.ResetHardAsync(batch.WorktreePath, cancellationToken);
            await _git.CleanWorkingTreeAsync(batch.WorktreePath, cancellationToken);
            worktreeReset = true;
        }

        // Re-queue any card the killed run stranded in Doing back to To Do so the
        // board tells the truth. The BatchId stays put so `run --resume` still picks
        // the card up, and the stale auto-run failure marker is cleared.
        var stuckCards = await db.Cards
            .Where(c => c.BatchId == batch.Id && c.LaneName == SystemLaneNames.Doing)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        if (stuckCards.Count > 0)
        {
            foreach (var card in stuckCards)
                card.LastAutoRunFailedAt = null;
            await db.SaveChangesAsync(cancellationToken);

            foreach (var card in stuckCards)
                await _sender.Send(
                    new UpdateCardCommand(card.Id, null, null, false, null, ToLaneName: SystemLaneNames.ToDo),
                    cancellationToken);
        }

        var lockCleared = BatchLock.DeleteLockFile(BatchLock.LockFilePath(batch.WorktreePath, batch.Id));

        return new RescueBatchResult(
            RescueBatchOutcome.Rescued,
            LockCleared: lockCleared,
            WorktreeReset: worktreeReset,
            DirtyPaths: dirtyPaths,
            RequeuedCardNumbers: stuckCards.Select(c => c.Number).ToList());
    }
}
