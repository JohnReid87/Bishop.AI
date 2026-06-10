namespace Bishop.Core;

public static class PerProjectSkills
{
    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "bish-tests",
    };

    public static bool IsPerProject(string skillName) => Allowlist.Contains(skillName);
}
