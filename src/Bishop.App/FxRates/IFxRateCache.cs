using Bishop.Core;

namespace Bishop.App.FxRates;

public interface IFxRateCache
{
    Task<FxRate?> GetAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task UpsertAsync(Guid workspaceId, decimal rate, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default);
}
