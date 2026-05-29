using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.RemoveBatch;

public sealed class RemoveBatchCommandHandler : IRequestHandler<RemoveBatchCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RemoveBatchCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(RemoveBatchCommand request, CancellationToken cancellationToken)
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
        if (batch.Status != BatchStatus.Closed)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' must be Closed to remove; current status is {batch.Status}.");

        db.Batches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
    }
}
