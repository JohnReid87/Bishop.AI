namespace Bishop.ViewModels.Findings;

internal static class FindingSeverityColor
{
    internal static string For(string? severity) =>
        (severity ?? string.Empty).ToLowerInvariant() switch
        {
            "critical" or "high" => "#c97a8a",
            "medium" or "med" => "#c4a85f",
            "low" or "info" => "#5fa89c",
            _ => "#9aa86a",
        };
}
