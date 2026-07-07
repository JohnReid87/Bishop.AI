namespace Bishop.Core.Skills;

/// <summary>
/// Classification helpers over <see cref="SkillCategory"/>.
/// </summary>
public static class SkillCategoryExtensions
{
    /// <summary>
    /// True for the review/analysis categories (<see cref="SkillCategory.Code"/>,
    /// <see cref="SkillCategory.Tests"/>, <see cref="SkillCategory.Review"/>) whose skills
    /// record findings and whose single UI home is the Monitoring view (run history +
    /// findings + "Run now"). Skills in these categories are excluded from the board's
    /// quick-launch flyouts so each skill has exactly one entry point.
    /// </summary>
    public static bool IsMonitored(this SkillCategory category)
        => category is SkillCategory.Code or SkillCategory.Tests or SkillCategory.Review;
}
