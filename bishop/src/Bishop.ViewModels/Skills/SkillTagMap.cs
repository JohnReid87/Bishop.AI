namespace Bishop.ViewModels.Skills;

internal static class SkillTagMap
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
        };

    public static string? GetTag(string skillName) =>
        !string.IsNullOrEmpty(skillName) && Map.TryGetValue(skillName, out var tag) ? tag : null;
}
