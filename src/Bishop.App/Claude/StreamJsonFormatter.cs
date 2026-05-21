using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Bishop.App.Claude;

public sealed class StreamJsonFormatter
{
    private const int MaxSummaryLength = 120;

    private int _toolUseCount;

    public string? Format(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("type", out var typeProp)
                || typeProp.ValueKind != JsonValueKind.String)
                return null;

            return typeProp.GetString() switch
            {
                "assistant" => FormatAssistant(root),
                "user" => FormatUser(root),
                "result" => FormatResult(root),
                _ => null,
            };
        }
    }

    private string? FormatAssistant(JsonElement root)
    {
        if (!TryGetContentArray(root, out var content))
            return null;

        var lines = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;
            if (!block.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) continue;
            var formatted = t.GetString() switch
            {
                "text" => FormatAssistantText(block),
                "tool_use" => FormatToolUse(block),
                _ => null,
            };
            if (!string.IsNullOrEmpty(formatted))
                lines.Add(formatted);
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string? FormatAssistantText(JsonElement block)
    {
        if (!block.TryGetProperty("text", out var textProp)
            || textProp.ValueKind != JsonValueKind.String)
            return null;
        var raw = textProp.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return $"… {Truncate(CollapseWhitespace(raw))}";
    }

    private string? FormatToolUse(JsonElement block)
    {
        if (!block.TryGetProperty("name", out var nameProp)
            || nameProp.ValueKind != JsonValueKind.String)
            return null;
        var name = nameProp.GetString()!;
        _toolUseCount++;

        var subject = ExtractToolSubject(name, block);
        if (string.IsNullOrEmpty(subject))
            return $"→ {name}";

        var separator = IsFilePathTool(name) ? ' ' : ':';
        return separator == ' '
            ? $"→ {name} {subject}"
            : $"→ {name}: {subject}";
    }

    private static bool IsFilePathTool(string name) => name switch
    {
        "Read" or "Edit" or "Write" or "NotebookEdit" => true,
        _ => false,
    };

    private static string? ExtractToolSubject(string toolName, JsonElement block)
    {
        if (!block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
            return null;

        var field = toolName switch
        {
            "Bash" or "PowerShell" => "command",
            "Read" or "Edit" or "Write" or "NotebookEdit" => "file_path",
            "Glob" or "Grep" => "pattern",
            "WebFetch" => "url",
            "WebSearch" => "query",
            "Task" or "Agent" => "description",
            "Skill" => "skill",
            "ToolSearch" => "query",
            _ => null,
        };
        if (field is null) return null;
        if (!input.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Truncate(CollapseWhitespace(raw));
    }

    private static string? FormatUser(JsonElement root)
    {
        if (!TryGetContentArray(root, out var content))
            return null;

        var lines = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;
            if (!block.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) continue;
            if (t.GetString() != "tool_result") continue;
            if (!block.TryGetProperty("is_error", out var err) || err.ValueKind != JsonValueKind.True)
                continue;

            var detail = ExtractToolResultText(block);
            var snippet = string.IsNullOrWhiteSpace(detail)
                ? "(no detail)"
                : Truncate(CollapseWhitespace(detail));
            lines.Add($"[error] {snippet}");
        }
        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string? ExtractToolResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content)) return null;
        if (content.ValueKind == JsonValueKind.String) return content.GetString();
        if (content.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                    return t.GetString();
            }
        }
        return null;
    }

    private string? FormatResult(JsonElement root)
    {
        string? duration = null;
        if (root.TryGetProperty("duration_ms", out var durProp)
            && durProp.ValueKind == JsonValueKind.Number
            && durProp.TryGetInt64(out var durMs))
        {
            duration = FormatDuration(TimeSpan.FromMilliseconds(durMs));
        }

        double? cost = null;
        if (root.TryGetProperty("total_cost_usd", out var costProp)
            && costProp.ValueKind == JsonValueKind.Number
            && costProp.TryGetDouble(out var c))
        {
            cost = c;
        }

        var parts = new List<string>
        {
            duration is null ? "done" : $"done in {duration}",
            $"{_toolUseCount} tool {(_toolUseCount == 1 ? "use" : "uses")}",
        };
        if (cost is not null)
            parts.Add($"${cost.Value.ToString("0.####", CultureInfo.InvariantCulture)}");

        return string.Join(", ", parts);
    }

    private static bool TryGetContentArray(JsonElement root, out JsonElement content)
    {
        content = default;
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            return false;
        if (!message.TryGetProperty("content", out var c) || c.ValueKind != JsonValueKind.Array)
            return false;
        content = c;
        return true;
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && !lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }
        if (lastWasSpace) sb.Length--;
        return sb.ToString();
    }

    private static string Truncate(string s)
        => s.Length > MaxSummaryLength ? s[..MaxSummaryLength] + "…" : s;

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 1)
            return $"{ts.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms";
        if (ts.TotalMinutes < 1)
            return $"{ts.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s";
        if (ts.TotalHours < 1)
            return $"{(int)ts.TotalMinutes}m{ts.Seconds}s";
        return $"{(int)ts.TotalHours}h{ts.Minutes}m";
    }
}
