namespace Bishop.App.FxRates;

public sealed class FxRateProvider : IFxRateProvider
{
    private readonly IFxRateClient _client;
    private readonly IFxRateCache _cache;
    private readonly Func<DateTimeOffset> _now;

    public FxRateProvider(IFxRateClient client, IFxRateCache cache)
        : this(client, cache, () => DateTimeOffset.UtcNow)
    {
    }

    public FxRateProvider(IFxRateClient client, IFxRateCache cache, Func<DateTimeOffset> now)
    {
        _client = client;
        _cache = cache;
        _now = now;
    }

    public async Task<decimal?> GetUsdToGbpAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var today = _now().UtcDateTime.Date;
        var cached = await _cache.GetAsync(workspaceId, cancellationToken);

        if (cached is not null && cached.FetchedAtUtc.UtcDateTime.Date == today)
            return cached.UsdToGbp;

        var fresh = await _client.FetchUsdToGbpAsync(cancellationToken);
        if (fresh is null)
            return cached?.UsdToGbp;

        await _cache.UpsertAsync(workspaceId, fresh.Value, _now().ToUniversalTime(), cancellationToken);
        return fresh;
    }

    public async Task<decimal?> RefreshUsdToGbpAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var fresh = await _client.FetchUsdToGbpAsync(cancellationToken);
        if (fresh is null)
            return null;

        await _cache.UpsertAsync(workspaceId, fresh.Value, _now().ToUniversalTime(), cancellationToken);
        return fresh;
    }
}
