using System.Text.Json;

namespace Bishop.App.FxRates;

public sealed class HttpFxRateClient : IFxRateClient
{
    private const string FetchPath = "latest?base=USD&symbols=GBP";
    private readonly HttpClient _http;

    public HttpFxRateClient(HttpClient http) => _http = http;

    public async Task<decimal?> FetchUsdToGbpAsync(CancellationToken cancellationToken = default)
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
}
