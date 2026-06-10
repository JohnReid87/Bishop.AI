namespace Bishop.ViewModels.Skills;

internal readonly record struct SkillRunStatus(
    string CommitsSince,
    string DotColor,
    string Tooltip,
    int SeverityRank)
{
    private static readonly SkillRunStatus NeverAudited = new("—", "#c97a8a", "Never audited", 2);
    private static readonly SkillRunStatus ShaGone = new("Re-audit", "#c97a8a", "Audit SHA is no longer reachable from HEAD", 2);

    internal static SkillRunStatus For(DateTimeOffset? lastRun, int? commitsSince, bool shaUnreachable)
    {
        if (lastRun is null) return NeverAudited;
        if (shaUnreachable) return ShaGone;

        var count = commitsSince ?? 0;
        return new(
            count.ToString(),
            count < 10 ? "#4a9e6a" : count < 50 ? "#c4a85f" : "#c97a8a",
            count < 10 ? "Fresh" : count < 50 ? "Getting stale" : "Stale — re-audit recommended",
            count < 10 ? 0 : count < 50 ? 1 : 2);
    }
}
