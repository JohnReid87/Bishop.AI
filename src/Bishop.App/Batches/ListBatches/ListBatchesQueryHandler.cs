using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.ListBatches;

public sealed class ListBatchesQueryHandler : IRequestHandler<ListBatchesQuery, IReadOnlyList<BatchSummary>>
{
    private readonly IBatchRepository _batches;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListBatchesQueryHandler(IBatchRepository batches, IDbContextFactory<BishopDbContext> dbFactory)
    {
        _batches = batches;
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<BatchSummary>> Handle(ListBatchesQuery request, CancellationToken cancellationToken)
    {
        var all = await _batches.ListAsync(cancellationToken);
        var visible = all.Where(b => b.Status != BatchStatus.Closed || b.GitHubPrUrl != null).ToList();

        if (visible.Count == 0)
            return [];

        var batchIds = visible.Select(b => b.Id).ToList();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var cardCounts = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId.HasValue && batchIds.Contains(c.BatchId!.Value))
            .GroupBy(c => c.BatchId!.Value)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BatchId, x => x.Count, cancellationToken);

        return visible.Select(b => new BatchSummary(b, cardCounts.GetValueOrDefault(b.Id, 0))).ToList();
    }
}
