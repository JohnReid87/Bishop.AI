using Bishop.Core.Skills;

namespace Bishop.App.Skills;

public static class SkillMenuBuilder
{
    public static SkillMenuItem[] Build(IEnumerable<InstalledSkill> skills, string scope)
    {
        var filtered = skills
            .Where(s => s.Scope.Contains(scope) && s.Command is not null)
            .ToArray();

        return filtered
            .Select((s, i) => new SkillMenuItem(s.Name, s, i < filtered.Length - 1))
            .ToArray();
    }
}
