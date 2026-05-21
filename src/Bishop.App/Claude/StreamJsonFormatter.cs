using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Bishop.App.Claude;

public sealed class StreamJsonFormatter
{
    private const int MaxSummaryLength = 120;

    private readonly Action<string>? _onStatus;
    private int _toolUseCount;

    public StreamJsonFormatter(Action<string>? onStatus = null)
    {
        _onStatus = onStatus;
    }

    public ClaudeRunTotals? Totals { get; private set; }

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
            switch (t.GetString())
            {
                case "text":
                    var cleaned = ExtractAssistantText(block);
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        _onStatus?.Invoke(cleaned);
                        lines.Add($"… {cleaned}");
                    }
                    break;
                case "tool_use":
                    _toolUseCount++;
                    if (_onStatus is not null)
                    {
                        var toolName = ReadToolName(block);
                        if (!string.IsNullOrEmpty(toolName))
                            _onStatus.Invoke($"Tool: {toolName}");
                    }
                    break;
            }
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string? ExtractAssistantText(JsonElement block)
    {
        if (!block.TryGetProperty("text", out var textProp)
            || textProp.ValueKind != JsonValueKind.String)
            return null;
        var raw = textProp.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Truncate(CollapseWhitespace(raw));
    }

    private static string? ReadToolName(JsonElement block)
    {
        if (block.TryGetProperty("name", out var nameProp)
            && nameProp.ValueKind == JsonValueKind.String)
            return nameProp.GetString();
        return null;
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

        decimal? cost = null;
        if (root.TryGetProperty("total_cost_usd", out var costProp)
            && costProp.ValueKind == JsonValueKind.Number
            && costProp.TryGetDecimal(out var c))
        {
            cost = c;
        }

        var (inputTokens, outputTokens) = ExtractUsage(root);

        if (cost is not null || inputTokens > 0 || outputTokens > 0)
            Totals = new ClaudeRunTotals(cost ?? 0m, inputTokens, outputTokens);

        var parts = new List<string>
        {
            duration is null ? "done" : $"done in {duration}",
            $"{_toolUseCount} tool {(_toolUseCount == 1 ? "use" : "uses")}",
        };
        if (cost is not null)
            parts.Add($"${cost.Value.ToString("0.####", CultureInfo.InvariantCulture)}");

        return string.Join(", ", parts);
    }

    private static (int InputTokens, int OutputTokens) ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return (0, 0);
        var input = ReadInt(usage, "input_tokens");
        var output = ReadInt(usage, "output_tokens");
        return (input, output);
    }

    private static int ReadInt(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var prop)
            && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetInt32(out var v))
        {
            return v;
        }
        return 0;
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
