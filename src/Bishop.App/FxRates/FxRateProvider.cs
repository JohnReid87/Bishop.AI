using System.Text.Json;
using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.FxRates;

public sealed class FxRateProvider : IFxRateProvider
{
    private const string FetchPath = "latest?base=USD&symbols=GBP";

    private readonly HttpClient _http;
    private readonly BishopDbContext _db;
    private readonly Func<DateTimeOffset> _now;

    public FxRateProvider(HttpClient http, BishopDbContext db)
        : this(http, db, () => DateTimeOffset.UtcNow)
    {
    }

    public FxRateProvider(HttpClient http, BishopDbContext db, Func<DateTimeOffset> now)
    {
        _http = http;
        _db = db;
        _now = now;
    }

    public async Task<decimal?> GetUsdToGbpAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var today = _now().UtcDateTime.Date;
        var cache = await _db.FxRates
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.WorkspaceId == workspaceId, cancellationToken);

        if (cache is not null && cache.FetchedAtUtc.UtcDateTime.Date == today)
            return cache.UsdToGbp;

        var fresh = await TryFetchAsync(cancellationToken);
        if (fresh is null)
            return cache?.UsdToGbp;

        await UpsertAsync(workspaceId, fresh.Value, cancellationToken);
        return fresh;
    }

    private async Task<decimal?> TryFetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(FetchPath, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (doc.RootElement.TryGetProperty("rates", out var rates)
                && rates.TryGetProperty("GBP", out var gbp)
                && gbp.TryGetDecimal(out var rate))
            {
                return rate;
            }
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private async Task UpsertAsync(Guid workspaceId, decimal rate, CancellationToken cancellationToken)
    {
        var existing = await _db.FxRates
            .FirstOrDefaultAsync(r => r.WorkspaceId == workspaceId, cancellationToken);

        var stamp = _now().ToUniversalTime();
        if (existing is null)
        {
            _db.FxRates.Add(new FxRate
            {
                WorkspaceId = workspaceId,
                UsdToGbp = rate,
                FetchedAtUtc = stamp
            });
        }
        else
        {
            existing.UsdToGbp = rate;
            existing.FetchedAtUtc = stamp;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
