using Bishop.Core.Skills;
using MediatR;
using static Bishop.Core.Skills.SkillCategory;

namespace Bishop.App.Skills.DiscoverSkills;

public sealed class DiscoverSkillsQueryHandler : IRequestHandler<DiscoverSkillsQuery, IReadOnlyList<InstalledSkill>>
{
    private readonly string _skillsRoot;

    public DiscoverSkillsQueryHandler()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "skills"))
    { }

    internal DiscoverSkillsQueryHandler(string skillsRoot) => _skillsRoot = skillsRoot;

    public Task<IReadOnlyList<InstalledSkill>> Handle(DiscoverSkillsQuery request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_skillsRoot))
            return Task.FromResult<IReadOnlyList<InstalledSkill>>([]);

        var skills = new List<InstalledSkill>();

        foreach (var dir in Directory.EnumerateDirectories(_skillsRoot))
        {
            var skillFile = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillFile))
                continue;

            var content = File.ReadAllText(skillFile);
            var (fm, body) = ParseFrontmatterAndBody(content);

            if (!fm.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                continue;

            fm.TryGetValue("description", out var description);
            fm.TryGetValue("bishop.scope", out var scope);
            fm.TryGetValue("bishop.command", out var command);
            fm.TryGetValue("bishop.stage", out var stage);
            fm.TryGetValue("bishop.stage_prompt", out var stagePrompt);
            fm.TryGetValue("bishop.stage_prefill", out var stagePrefill);
            fm.TryGetValue("bishop.category", out var category);

            skills.Add(new InstalledSkill(
                name,
                description ?? string.Empty,
                ParseScope(scope),
                string.IsNullOrWhiteSpace(command) ? null : command,
                string.Equals(stage, "true", StringComparison.OrdinalIgnoreCase),
                string.IsNullOrWhiteSpace(stagePrompt) ? null : stagePrompt,
                ParseStagePrefill(stagePrefill),
                body,
                skillFile,
                ParseCategory(category)));
        }

        return Task.FromResult<IReadOnlyList<InstalledSkill>>(skills);
    }

    private static IReadOnlyList<string> ParseScope(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static SkillCategory ParseCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Other;

        return raw.Trim().ToLowerInvariant() switch
        {
            "review"    => Review,
            "discuss"   => Discuss,
            "execute"   => Execute,
            "setup"     => Setup,
            _           => Other,
        };
    }

    private static string? ParseStagePrefill(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw;
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1].Replace("\\n", "\n");

        return value;
    }

    private static (Dictionary<string, string> Frontmatter, string Body) ParseFrontmatterAndBody(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = content.ReplaceLineEndings("\n").Split('\n');

        if (lines.Length < 2 || lines[0].Trim() != "---")
            return (result, string.Empty);

        var closingIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                closingIndex = i;
                break;
            }

            var colonIdx = lines[i].IndexOf(':');
            if (colonIdx <= 0)
                continue;

            var key = lines[i][..colonIdx].Trim();
            var value = lines[i][(colonIdx + 1)..].Trim();
            result[key] = value;
        }

        var body = closingIndex >= 0 && closingIndex < lines.Length - 1
            ? string.Join("\n", lines.Skip(closingIndex + 1)).TrimStart('\n')
            : string.Empty;

        return (result, body);
    }
}
