using Bishop.Core.Skills;
using MediatR;

namespace Bishop.App.Skills.DiscoverSkills;

public sealed class DiscoverSkillsQueryHandler : IRequestHandler<DiscoverSkillsQuery, IReadOnlyList<InstalledSkill>>
{
    private static readonly string SkillsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "skills");

    public Task<IReadOnlyList<InstalledSkill>> Handle(DiscoverSkillsQuery request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(SkillsRoot))
            return Task.FromResult<IReadOnlyList<InstalledSkill>>([]);

        var skills = new List<InstalledSkill>();

        foreach (var dir in Directory.EnumerateDirectories(SkillsRoot))
        {
            var skillFile = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillFile))
                continue;

            var content = File.ReadAllText(skillFile);
            var fm = ParseFrontmatter(content);

            if (!fm.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                continue;

            fm.TryGetValue("description", out var description);
            fm.TryGetValue("bishop.scope", out var scope);
            fm.TryGetValue("bishop.command", out var command);

            skills.Add(new InstalledSkill(
                name,
                description ?? string.Empty,
                string.IsNullOrWhiteSpace(scope) ? null : scope,
                string.IsNullOrWhiteSpace(command) ? null : command));
        }

        return Task.FromResult<IReadOnlyList<InstalledSkill>>(skills);
    }

    private static Dictionary<string, string> ParseFrontmatter(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = content.ReplaceLineEndings("\n").Split('\n');

        if (lines.Length < 2 || lines[0].Trim() != "---")
            return result;

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
                break;

            var colonIdx = lines[i].IndexOf(':');
            if (colonIdx <= 0)
                continue;

            var key = lines[i][..colonIdx].Trim();
            var value = lines[i][(colonIdx + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }
}
