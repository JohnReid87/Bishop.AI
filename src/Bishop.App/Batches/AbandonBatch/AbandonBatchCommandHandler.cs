using System.Data;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.AbandonBatch;

internal sealed class AbandonBatchCommandHandler : IRequestHandler<AbandonBatchCommand, AbandonBatchResult>
{
    private readonly IGitCli _git;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public AbandonBatchCommandHandler(
        IGitCli git,
        IDbContextFactory<BishopDbContext> dbFactory,
        TimeProvider timeProvider)
    {
        _git = git;
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task<AbandonBatchResult> Handle(AbandonBatchCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var matches = await db.Batches.ByName(request.Name).ToListAsync(cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];
        if (batch.Status == BatchStatus.Closed)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' is already Closed and cannot be abandoned.");

        var cardCount = await db.Cards.CountAsync(c => c.BatchId == batch.Id, cancellationToken);

        // Bulk-move every card in the batch back to "To Do", clear BatchId, and
        // reopen any card sitting in "Done" so it doesn't return as closed. One
        // SQL statement regardless of card count; shares the transaction with
        // the Close() below so the abandon is all-or-nothing.
        await db.Cards
            .Where(c => c.BatchId == batch.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LaneName, SystemLaneNames.ToDo)
                .SetProperty(c => c.BatchId, (Guid?)null)
                .SetProperty(c => c.IsClosed, c => c.LaneName == SystemLaneNames.Done ? false : c.IsClosed),
                cancellationToken);

        batch.Close(BatchClosedReason.Abandoned, _timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _git.RemoveWorktreeAsync(request.WorkspacePath, batch.WorktreePath, cancellationToken);

        return new AbandonBatchResult(cardCount);
    }
}
