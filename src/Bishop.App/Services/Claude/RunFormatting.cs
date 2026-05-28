using System.Globalization;

namespace Bishop.App.Services.Claude;

public static class RunFormatting
{
    public static string FormatTokens(int n)
    {
        if (n < 1000)
            return n.ToString(CultureInfo.InvariantCulture);
        return (n / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";
    }

    public static string? FormatTokenSuffix(int input, int output, int cache = 0)
    {
        if (input == 0 && output == 0)
            return null;
        var suffix = $"{FormatTokens(input)}↑ {FormatTokens(output)}↓";
        if (cache > 0)
            suffix += $" · cache {FormatTokens(cache)}";
        return suffix;
    }

    public static string? FormatFinalTokenSegment(ClaudeRunTotals? totals)
    {
        if (totals is null || (totals.InputTokens == 0 && totals.OutputTokens == 0))
            return null;
        var segment = $"{FormatTokens(totals.InputTokens)} in / {FormatTokens(totals.OutputTokens)} out";
        if (totals.CacheReadTokens > 0 || totals.CacheCreationTokens > 0)
            segment += $" · cache {FormatTokens(totals.CacheReadTokens)} read / {FormatTokens(totals.CacheCreationTokens)} written";
        return segment;
    }

    public static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 1)
            return $"{ts.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms";
        if (ts.TotalMinutes < 1)
            return $"{ts.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s";
        if (ts.TotalHours < 1)
            return $"{(int)ts.TotalMinutes}m{ts.Seconds}s";
        return $"{(int)ts.TotalHours}h{ts.Minutes}m";
    }
}
