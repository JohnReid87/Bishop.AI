using System.Data;
using Bishop.Core;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Data;

public sealed class BatchRepository : IBatchRepository
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public BatchRepository(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Batch> CreateAsync(string name, string branchName, string baseBranch, string worktreePath, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            Name = name,
            BranchName = branchName,
            BaseBranch = baseBranch,
            WorktreePath = worktreePath,
            Status = BatchStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<Batch?> GetAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
    }

    public async Task<Batch?> GetByBranchNameAsync(string branchName, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Batches.FirstOrDefaultAsync(b => b.BranchName == branchName, cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batches = await db.Batches.ToListAsync(cancellationToken);
        return batches.OrderBy(b => b.CreatedAt).ToList();
    }

    public async Task<Batch> TransitionToWorkingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");
        if (batch.Status != BatchStatus.Open)
            throw new InvalidOperationException($"Batch must be Open to transition to Working; current status is {batch.Status}.");
        batch.Status = BatchStatus.Working;
        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<Batch> CloseAsync(Guid batchId, BatchClosedReason reason, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");
        if (batch.Status == BatchStatus.Closed)
            throw new InvalidOperationException($"Batch {batchId} is already Closed.");
        batch.Status = BatchStatus.Closed;
        batch.ClosedReason = reason;
        batch.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task AssignCardAsync(Guid batchId, Guid cardId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");
        if (batch.Status == BatchStatus.Closed)
            throw new InvalidOperationException($"Cannot assign a card to a Closed batch.");

        var card = await db.Cards
            .Include(c => c.Batch)
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {cardId} not found.");

        if (card.BatchId is not null && card.Batch!.Status != BatchStatus.Closed)
            throw new InvalidOperationException(
                $"Card {cardId} is already assigned to batch {card.BatchId} which is not Closed.");

        card.BatchId = batchId;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");
        db.Batches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
    }
}
