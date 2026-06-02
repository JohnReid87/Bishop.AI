namespace Bishop.ViewModels.Findings;

internal static class FindingSeverityRanker
{
    public static int Rank(FindingItemViewModel f) => (f.Severity ?? string.Empty).ToLowerInvariant() switch
    {
        "critical" => 0,
        "high" => 1,
        "medium" or "med" => 2,
        "low" => 3,
        "info" => 4,
        _ => 5,
    };
}
