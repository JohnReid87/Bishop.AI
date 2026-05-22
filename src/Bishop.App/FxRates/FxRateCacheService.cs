using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.FxRates;

public sealed class FxRateCacheService : IFxRateCache
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public FxRateCacheService(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<FxRate?> GetAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FxRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.WorkspaceId == workspaceId, cancellationToken);
    }

    public async Task UpsertAsync(Guid workspaceId, decimal rate, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.FxRates
            .FirstOrDefaultAsync(r => r.WorkspaceId == workspaceId, cancellationToken);

        if (existing is null)
        {
            db.FxRates.Add(new FxRate
            {
                WorkspaceId = workspaceId,
                UsdToGbp = rate,
                FetchedAtUtc = fetchedAt
            });
        }
        else
        {
            existing.UsdToGbp = rate;
            existing.FetchedAtUtc = fetchedAt;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
