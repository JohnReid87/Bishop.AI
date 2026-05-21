namespace Bishop.App.FxRates;

public interface IFxRateProvider
{
    Task<decimal?> GetUsdToGbpAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
