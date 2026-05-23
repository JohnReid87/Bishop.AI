using System.Globalization;
using System.Text;

namespace Bishop.App.Services.Claude;

public static class ClaudeTotalsFormatter
{
    public static string? Format(int inputTokens, int outputTokens, int runCount)
    {
        if (inputTokens == 0 && outputTokens == 0 && runCount == 0)
            return null;

        var sb = new StringBuilder("Claude: ");
        sb.Append(runCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(runCount == 1 ? " run, " : " runs, ");
        sb.Append(RunFormatting.FormatTokens(inputTokens));
        sb.Append(" in / ");
        sb.Append(RunFormatting.FormatTokens(outputTokens));
        sb.Append(" out");
        return sb.ToString();
    }
}
