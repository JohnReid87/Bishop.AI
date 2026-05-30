namespace Bishop.ViewModels.Skills;

public sealed class SkillTagMap : ISkillTagMap
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bish-arch"] = "arch",
            ["bish-security"] = "security",
            ["bish-tests"] = "test",
            ["bish-coverage"] = "test",
            ["bish-dead-code"] = "chore",
            ["bish-audit-docs"] = "docs",
            ["bish-triage"] = "bug",
        };

    public string? GetTag(string skillName) =>
        !string.IsNullOrEmpty(skillName) && Map.TryGetValue(skillName, out var tag) ? tag : null;
}
