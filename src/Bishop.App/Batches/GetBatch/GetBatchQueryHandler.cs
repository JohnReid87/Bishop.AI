using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.GetBatch;

public sealed class GetBatchQueryHandler : IRequestHandler<GetBatchQuery, GetBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetBatchQueryHandler(IBatchRepository batches, IDbContextFactory<BishopDbContext> dbFactory)
    {
        _batches = batches;
        _dbFactory = dbFactory;
    }

    public async Task<GetBatchResult> Handle(GetBatchQuery request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);

        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        return new GetBatchResult(batch, cards);
    }
}
