namespace Bishop.ViewModels;

public sealed class SkillRunRowViewModel
{
    public string SkillName { get; }
    public string LastRunText { get; }
    public string CommitsSinceText { get; }
    public string StatusDotColor { get; }
    public string StatusTooltip { get; }
    public int SeverityRank { get; }

    public SkillRunRowViewModel(string skillName, DateTimeOffset? lastRun, int? commitsSince, bool shaUnreachable)
    {
        SkillName = skillName;
        LastRunText = lastRun is null ? "Never" : FormatRelativeTime(lastRun.Value);

        if (lastRun is null)
        {
            CommitsSinceText = "—";
            StatusDotColor = "#ff5555";
            StatusTooltip = "Never audited";
            SeverityRank = 2;
        }
        else if (shaUnreachable)
        {
            CommitsSinceText = "Re-audit";
            StatusDotColor = "#ff5555";
            StatusTooltip = "Audit SHA is no longer reachable from HEAD";
            SeverityRank = 2;
        }
        else
        {
            var count = commitsSince ?? 0;
            CommitsSinceText = count.ToString();
            StatusDotColor = count < 10 ? "#4a9e6a" : count < 50 ? "#c4944f" : "#ff5555";
            StatusTooltip = count < 10 ? "Fresh" : count < 50 ? "Getting stale" : "Stale — re-audit recommended";
            SeverityRank = count < 10 ? 0 : count < 50 ? 1 : 2;
        }
    }

    private static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"{(int)elapsed.TotalDays}d ago";
        return $"{(int)(elapsed.TotalDays / 30)}mo ago";
    }
}
