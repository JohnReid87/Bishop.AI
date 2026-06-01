using System.Data;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.AddCardToBatch;

internal sealed class AddCardToBatchCommandHandler : IRequestHandler<AddCardToBatchCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AddCardToBatchCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(AddCardToBatchCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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

        await BatchAssignment.AssignAsync(db, batch, request.CardId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
