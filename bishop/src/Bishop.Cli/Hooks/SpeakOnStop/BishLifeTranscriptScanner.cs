using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bishop.Cli.Hooks.SpeakOnStop;

/// <summary>
/// Reads a Claude Code transcript JSONL file and decides whether the most recent
/// assistant message should be spoken aloud — i.e. whether the active skill is
/// one of the opted-in <c>bish-life-*</c> skills.
/// </summary>
internal static class BishLifeTranscriptScanner
{
    private static readonly HashSet<string> SpeakingSkills = new(StringComparer.Ordinal)
    {
        "bish-life-standup",
        "bish-life-add",
    };
    private static readonly Regex CommandNameRegex = new(@"<command-name>/?([a-z0-9-]+)</command-name>", RegexOptions.Compiled);
    private static readonly Regex NoSpeakRegex = new(@"<!--\s*no-speak\s*-->.*?<!--\s*/no-speak\s*-->", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex EmphasisRegex = new(@"(\*\*|\*|_)(.+?)\1", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex BacktickRegex = new(@"`+([^`]*)`+", RegexOptions.Compiled);
    private static readonly Regex LeadingHeadingRegex = new(@"^[ \t]*#{1,6}[ \t]+", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LeadingBulletRegex = new(@"^[ \t]*[-*•][ \t]+", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex SeparatorCharRegex = new(@"[▸›]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex BlankLinesRegex = new(@"(\r?\n[ \t]*){2,}", RegexOptions.Compiled);

    public static bool TryGetTextToSpeak(string transcriptPath, out string text)
    {
        text = string.Empty;
        if (!File.Exists(transcriptPath))
            return false;

        string? activeSkill = null;
        string? lastAssistantText = null;

        foreach (var line in File.ReadLines(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonNode? node;
            try { node = JsonNode.Parse(line); }
            catch { continue; }
            if (node is null) continue;

            var type = node["type"]?.GetValue<string>();
            if (type == "user")
            {
                var userText = ExtractTextContent(node["message"]);
                var match = CommandNameRegex.Match(userText);
                if (match.Success)
                {
                    activeSkill = match.Groups[1].Value;
                    lastAssistantText = null; // new skill turn — discard prior assistant text
                }
            }
            else if (type == "assistant")
            {
                var assistantText = ExtractTextContent(node["message"]);
                if (!string.IsNullOrWhiteSpace(assistantText))
                    lastAssistantText = assistantText;
            }
        }

        if (activeSkill is null || !SpeakingSkills.Contains(activeSkill) || string.IsNullOrWhiteSpace(lastAssistantText))
            return false;

        var cleaned = StripForSpeech(lastAssistantText);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        text = cleaned;
        return true;
    }

    internal static string StripForSpeech(string input)
    {
        var s = NoSpeakRegex.Replace(input, string.Empty);
        s = LeadingHeadingRegex.Replace(s, string.Empty);
        s = LeadingBulletRegex.Replace(s, string.Empty);
        s = BacktickRegex.Replace(s, "$1");
        // Apply emphasis twice so **_x_** style nesting is fully unwrapped.
        s = EmphasisRegex.Replace(s, "$2");
        s = EmphasisRegex.Replace(s, "$2");
        s = SeparatorCharRegex.Replace(s, " ");
        s = WhitespaceRegex.Replace(s, " ");
        s = BlankLinesRegex.Replace(s, "\n");
        return s.Trim();
    }

    private static string ExtractTextContent(JsonNode? message)
    {
        if (message is null) return string.Empty;
        var content = message["content"];
        if (content is JsonArray arr)
        {
            var parts = new List<string>();
            foreach (var item in arr)
            {
                if (item?["type"]?.GetValue<string>() == "text")
                {
                    var t = item["text"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(t))
                        parts.Add(t);
                }
            }
            return string.Join("\n", parts);
        }
        if (content is JsonValue v && v.TryGetValue<string>(out var s))
            return s;
        return string.Empty;
    }
}
