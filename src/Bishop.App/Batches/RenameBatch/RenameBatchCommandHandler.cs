using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.RenameBatch;

public sealed class RenameBatchCommandHandler : IRequestHandler<RenameBatchCommand, Batch>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RenameBatchCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Batch> Handle(RenameBatchCommand request, CancellationToken cancellationToken)
    {
        var trimmed = request.NewName.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Batch name cannot be empty.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var matches = await db.Batches.ByName(request.Name).ToListAsync(cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        if (batch.Name == trimmed)
            return batch;

        var conflicts = await db.Batches.ByName(trimmed).ToListAsync(cancellationToken);
        if (conflicts.Any(b => b.Id != batch.Id && b.Status != BatchStatus.Closed))
            throw new InvalidOperationException($"An active batch named '{trimmed}' already exists.");

        batch.Name = trimmed;
        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }
}
