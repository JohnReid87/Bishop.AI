using Bishop.Core.Skills;
using static Bishop.Core.Skills.SkillCategory;

namespace Bishop.App.Skills.DiscoverSkills;

internal static class InstalledSkillFactory
{
    private static readonly IReadOnlyDictionary<string, SkillCategory> _categoryMap =
        new Dictionary<string, SkillCategory>
        {
            ["code"]    = Code,
            ["tests"]   = Tests,
            ["review"]  = Review,
            ["discuss"] = Discuss,
            ["execute"] = Execute,
            ["setup"]   = Setup,
            ["meta"]    = Meta,
        };

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

    private static SkillCategory ParseCategory(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && _categoryMap.TryGetValue(raw.Trim().ToLowerInvariant(), out var cat)
            ? cat
            : Other;

    private static string? ParseStagePrefill(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw[1..^1].Replace("\\n", "\n");

        return raw;
    }
}
