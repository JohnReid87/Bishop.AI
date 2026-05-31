using Bishop.Core.Skills;
using static Bishop.Core.Skills.SkillCategory;

namespace Bishop.App.Skills.DiscoverSkills;

internal static class InstalledSkillFactory
{
    public static InstalledSkill? TryCreate(SkillMdDocument doc, string sourcePath)
    {
        var fm = doc.Frontmatter;

        if (!fm.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        return new InstalledSkill(
            name,
            GetString(fm, "description") ?? string.Empty,
            ParseScope(GetString(fm, "bishop.scope")),
            GetString(fm, "bishop.command"),
            GetBool(fm, "bishop.stage"),
            GetString(fm, "bishop.stage_prompt"),
            ParseStagePrefill(GetRaw(fm, "bishop.stage_prefill")),
            doc.Body,
            sourcePath,
            ParseCategory(GetRaw(fm, "bishop.category")),
            GetBool(fm, "bishop.stage_projects"),
            GetBool(fm, "bishop.stage_file_picker"));
    }

    private static string? GetRaw(IReadOnlyDictionary<string, string> fm, string key) =>
        fm.TryGetValue(key, out var value) ? value : null;

    private static string? GetString(IReadOnlyDictionary<string, string> fm, string key)
    {
        var raw = GetRaw(fm, key);
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> fm, string key) =>
        string.Equals(GetRaw(fm, key), "true", StringComparison.OrdinalIgnoreCase);

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
            "code"      => Code,
            "tests"     => Tests,
            "review"    => Review,
            "discuss"   => Discuss,
            "execute"   => Execute,
            "setup"     => Setup,
            "meta"      => Meta,
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
}
