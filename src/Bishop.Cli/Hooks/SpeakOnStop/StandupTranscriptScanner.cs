using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bishop.Cli.Hooks.SpeakOnStop;

/// <summary>
/// Reads a Claude Code transcript JSONL file and decides whether the most recent
/// assistant message should be spoken aloud — i.e. whether the active skill is
/// <c>bish-life-standup</c>.
/// </summary>
internal static class StandupTranscriptScanner
{
    private const string StandupSkillName = "bish-life-standup";
    private static readonly Regex CommandNameRegex = new(@"<command-name>/?([a-z0-9-]+)</command-name>", RegexOptions.Compiled);

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

        if (activeSkill != StandupSkillName || string.IsNullOrWhiteSpace(lastAssistantText))
            return false;

        text = lastAssistantText;
        return true;
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
