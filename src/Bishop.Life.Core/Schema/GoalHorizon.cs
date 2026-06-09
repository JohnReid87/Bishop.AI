using System.Text.RegularExpressions;

namespace Bishop.Life.Core.Schema;

public static class GoalHorizon
{
    public const string Month = "month";
    public const string Year = "year";
    public const string Beyond = "beyond";

    private static readonly Regex YearMonth = new(@"^(\d{4})-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    public static bool IsValid(string? horizon)
        => horizon is null || YearMonth.IsMatch(horizon);

    public static string Bucket(string? horizon, DateOnly today)
    {
        if (string.IsNullOrEmpty(horizon)) return Beyond;
        var m = YearMonth.Match(horizon);
        if (!m.Success) return Beyond;

        var y = int.Parse(m.Groups[1].Value);
        var mo = int.Parse(m.Groups[2].Value);
        var diff = (y - today.Year) * 12 + (mo - today.Month);
        if (diff <= 3) return Month;
        if (y == today.Year) return Year;
        return Beyond;
    }
}
