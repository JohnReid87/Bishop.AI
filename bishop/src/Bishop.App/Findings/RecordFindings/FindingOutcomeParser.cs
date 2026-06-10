using System.Globalization;

namespace Bishop.App.Findings.RecordFindings;

internal static class FindingOutcomeParser
{
    public static (string Status, int? CardNumber) Parse(string outcome)
    {
        if (outcome == "dismissed") return ("dismissed", null);
        if (outcome.StartsWith("carded:#", StringComparison.Ordinal)
            && int.TryParse(outcome.AsSpan(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && n > 0)
            return ("carded", n);
        // "parked" and any other validator-accepted value land on pending.
        return ("pending", null);
    }
}
