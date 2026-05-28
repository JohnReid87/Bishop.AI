using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Bishop.App.Batches.ReconcileOrphanedBatches;

public sealed class ReconcileOrphanedBatchesCommandHandler : IRequestHandler<ReconcileOrphanedBatchesCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private static readonly TimeSpan s_lockStaleness = TimeSpan.FromMinutes(5);

    public ReconcileOrphanedBatchesCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task Handle(ReconcileOrphanedBatchesCommand request, CancellationToken cancellationToken)
    {
        await using var listDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var allBatches = await listDb.Batches.AsNoTracking().ToListAsync(cancellationToken);
        var workingBatches = allBatches.Where(b => b.Status == BatchStatus.Working).ToList();
        if (workingBatches.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        var orphanedIds = workingBatches
            .Where(b => IsOrphaned(b.WorktreePath, b.Id, now))
            .Select(b => b.Id)
            .ToList();

        if (orphanedIds.Count == 0)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var orphanedCards = await db.Cards
            .Where(c => c.BatchId.HasValue
                && orphanedIds.Contains(c.BatchId.Value)
                && c.LaneName == SystemLaneNames.Doing
                && c.LastAutoRunFailedAt == null)
            .ToListAsync(cancellationToken);

        if (orphanedCards.Count == 0)
            return;

        var failedAt = DateTimeOffset.UtcNow;
        foreach (var card in orphanedCards)
            card.LastAutoRunFailedAt = failedAt;

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static bool IsOrphaned(string worktreePath, Guid batchId, DateTimeOffset now)
    {
        var lockPath = Path.Combine(worktreePath, ".bishop", $"batch-{batchId}.lock");
        if (!File.Exists(lockPath))
            return true;

        try
        {
            var parts = File.ReadAllText(lockPath).Split('\t');
            if (parts.Length < 2)
                return true;

            if (DateTimeOffset.TryParse(parts[1], out var timestamp) && now - timestamp > s_lockStaleness)
                return true;

            return int.TryParse(parts[0], out var pid) && !IsProcessAlive(pid);
        }
        catch
        {
            // intentional: unreadable lock file treated as orphaned
            return true;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            // intentional: GetProcessById throws if pid not found; absence means not alive
            return false;
        }
    }
}
