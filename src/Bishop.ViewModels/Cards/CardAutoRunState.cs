namespace Bishop.ViewModels.Cards;

internal readonly record struct CardAutoRunState(
    bool FailedIndicatorVisible,
    string FailedTooltip,
    bool SucceededIndicatorVisible,
    string SucceededTooltip)
{
    internal static CardAutoRunState For(DateTimeOffset? failedAt, DateTimeOffset? succeededAt)
    {
        var failedVisible = failedAt.HasValue && (!succeededAt.HasValue || failedAt > succeededAt);
        var succeededVisible = succeededAt.HasValue && (!failedAt.HasValue || succeededAt >= failedAt);
        return new(
            failedVisible,
            failedVisible ? $"Auto-run failed at {failedAt!.Value:yyyy-MM-dd HH:mm}" : string.Empty,
            succeededVisible,
            succeededVisible ? $"Auto-run succeeded at {succeededAt!.Value:yyyy-MM-dd HH:mm}" : string.Empty);
    }
}
