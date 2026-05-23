using Bishop.Core.Skills;

namespace Bishop.App.Skills;

public static class SkillMenuBuilder
{
    private static readonly SkillCategory[] CategoryOrder =
        [SkillCategory.Review, SkillCategory.Discuss, SkillCategory.Execute, SkillCategory.Setup, SkillCategory.Other];

    public static SkillMenuItem[] Build(IEnumerable<InstalledSkill> skills, string scope)
    {
        var filtered = skills
            .Where(s => s.Scope.Contains(scope) && s.Command is not null)
            .ToArray();

        if (filtered.Length == 0)
            return [];

        var groups = filtered
            .GroupBy(s => s.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Name).ToArray());

        var nonEmptyCategories = CategoryOrder.Where(groups.ContainsKey).ToArray();
        var result = new List<SkillMenuItem>();

        for (var gi = 0; gi < nonEmptyCategories.Length; gi++)
        {
            var category = nonEmptyCategories[gi];
            var categorySkills = groups[category];
            var isLastGroup = gi == nonEmptyCategories.Length - 1;

            for (var si = 0; si < categorySkills.Length; si++)
            {
                var s = categorySkills[si];
                var isLastInGroup = si == categorySkills.Length - 1;
                var groupHeader = si == 0 ? category.ToString().ToUpperInvariant() : null;
                result.Add(new SkillMenuItem(s.Name, s, isLastInGroup && !isLastGroup, groupHeader));
            }
        }

        return [.. result];
    }
}
