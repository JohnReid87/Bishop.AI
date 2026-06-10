using System.Globalization;
using System.Text;
using Bishop.App.Skills;

namespace Bishop.App.Services.Claude;

public static class ClaudeCostFormatter
{
    /// <summary>
    /// Renders a USD amount with a minimum of two decimals and up to four when the amount
    /// needs them — so dollar figures stay clean ($0.42) while small per-run costs keep
    /// their precision ($0.0143) instead of collapsing to "$0.01".
    /// </summary>
    public static string FormatUsd(decimal costUsd)
        => "$" + costUsd.ToString("0.00##", CultureInfo.InvariantCulture);

    /// <summary>
    /// The cost finding appended to a card after an auto-run, e.g.
    /// <code>
    /// **Auto-run cost (est.):** $0.42
    /// Sonnet 4.6 · reported by agent · 1.2k in / 18.0k out · cache 0.9k read / 12.0k written
    /// </code>
    /// Returns null when there is no agent-reported cost to show.
    /// </summary>
    public static string? FormatCardFinding(string modelId, ClaudeRunTotals? totals)
    {
        if (totals is null || totals.CostUsd <= 0m)
            return null;

        var sb = new StringBuilder();
        sb.Append("**Auto-run cost (est.):** ");
        sb.Append(FormatUsd(totals.CostUsd));
        sb.Append('\n');
        sb.Append(ClaudeModels.DisplayFor(modelId));
        sb.Append(" · reported by agent");

        var detail = RunFormatting.FormatFinalTokenSegment(totals);
        if (detail is not null)
        {
            sb.Append(" · ");
            sb.Append(detail);
        }

        return sb.ToString();
    }
}
