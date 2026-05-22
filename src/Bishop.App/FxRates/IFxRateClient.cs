namespace Bishop.App.FxRates;

public interface IFxRateClient
{
    Task<decimal?> FetchUsdToGbpAsync(CancellationToken cancellationToken = default);
}
