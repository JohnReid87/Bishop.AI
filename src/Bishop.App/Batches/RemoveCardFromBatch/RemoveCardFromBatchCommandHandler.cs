using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.RemoveCardFromBatch;

public sealed class RemoveCardFromBatchCommandHandler : IRequestHandler<RemoveCardFromBatchCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RemoveCardFromBatchCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(RemoveCardFromBatchCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var matches = await db.Batches.ByName(request.BatchName).ToListAsync(cancellationToken);

        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.BatchName}' found.");

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.BatchName}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        if (batch.Status != BatchStatus.Open)
            throw new InvalidOperationException(
                $"Batch '{batch.Name}' is {batch.Status} — only Open batches accept card changes.");

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId && c.BatchId == batch.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} is not assigned to batch {batch.Id}.");

        card.BatchId = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
