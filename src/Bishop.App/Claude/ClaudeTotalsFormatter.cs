using System.Globalization;
using System.Text;

namespace Bishop.App.Claude;

public static class ClaudeTotalsFormatter
{
    public static string? Format(
        decimal costUsd,
        int inputTokens,
        int outputTokens,
        int runCount,
        decimal? usdToGbpRate)
    {
        if (costUsd == 0m && inputTokens == 0 && outputTokens == 0 && runCount == 0)
            return null;

        var sb = new StringBuilder("Claude: $");
        sb.Append(costUsd.ToString("0.00", CultureInfo.InvariantCulture));
        if (usdToGbpRate is not null)
        {
            sb.Append(" (£");
            sb.Append((costUsd * usdToGbpRate.Value).ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(')');
        }
        sb.Append(" (");
        sb.Append(runCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(runCount == 1 ? " run, " : " runs, ");
        sb.Append(RunFormatting.FormatTokens(inputTokens));
        sb.Append(" in / ");
        sb.Append(RunFormatting.FormatTokens(outputTokens));
        sb.Append(" out)");
        return sb.ToString();
    }
}
