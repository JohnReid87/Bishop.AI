using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.FxRates;

public sealed class FxRateCacheService : IFxRateCache
{
    private readonly BishopDbContext _db;

    public FxRateCacheService(BishopDbContext db) => _db = db;

    public Task<FxRate?> GetAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => _db.FxRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.WorkspaceId == workspaceId, cancellationToken);

    public async Task UpsertAsync(Guid workspaceId, decimal rate, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _db.FxRates
            .FirstOrDefaultAsync(r => r.WorkspaceId == workspaceId, cancellationToken);

        if (existing is null)
        {
            _db.FxRates.Add(new FxRate
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
        await _db.SaveChangesAsync(cancellationToken);
    }
}
