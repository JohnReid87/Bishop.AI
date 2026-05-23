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

    public static string? FormatTokenSuffix(int input, int output)
    {
        if (input == 0 && output == 0)
            return null;
        return $"{FormatTokens(input)}↑ {FormatTokens(output)}↓";
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
