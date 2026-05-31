namespace Bishop.App.Skills.DiscoverSkills;

internal sealed record SkillMdDocument(IReadOnlyDictionary<string, string> Frontmatter, string Body);

internal static class SkillMdParser
{
    private static readonly SkillMdDocument Empty =
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), string.Empty);

    public static SkillMdDocument Parse(string content)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n');

        if (lines.Length < 2 || lines[0].Trim() != "---")
            return Empty;

        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var closingIndex = ReadFrontmatter(lines, frontmatter);
        var body = ExtractBody(lines, closingIndex);

        return new SkillMdDocument(frontmatter, body);
    }

    private static int ReadFrontmatter(string[] lines, Dictionary<string, string> frontmatter)
    {
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
                return i;

            TryAddEntry(lines[i], frontmatter);
        }

        return -1;
    }

    private static void TryAddEntry(string line, Dictionary<string, string> frontmatter)
    {
        var colonIdx = line.IndexOf(':');
        if (colonIdx <= 0)
            return;

        var key = line[..colonIdx].Trim();
        var value = line[(colonIdx + 1)..].Trim();
        frontmatter[key] = value;
    }

    private static string ExtractBody(string[] lines, int closingIndex)
    {
        if (closingIndex < 0 || closingIndex >= lines.Length - 1)
            return string.Empty;

        return string.Join("\n", lines.Skip(closingIndex + 1)).TrimStart('\n');
    }
}
