using System.Text.Json;

namespace Bishop.App.Services.Claude;

internal static class UserBlockFormatter
{
    public static string? Format(JsonElement root)
    {
        if (!JsonReadHelpers.TryGetMessage(root, out var message))
            return null;
        if (!JsonReadHelpers.TryGetContentArray(message, out var content))
            return null;

        var lines = CollectErrorLines(content);
        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static List<string> CollectErrorLines(JsonElement content)
    {
        var lines = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            var line = FormatErrorBlock(block);
            if (line is not null) lines.Add(line);
        }
        return lines;
    }

    private static string? FormatErrorBlock(JsonElement block)
    {
        if (!IsErrorToolResult(block)) return null;
        var detail = ExtractToolResultText(block);
        return $"[error] {FormatDetail(detail)}";
    }

    private static bool IsErrorToolResult(JsonElement block)
    {
        if (block.ValueKind != JsonValueKind.Object) return false;
        if (!block.TryGetProperty("type", out var t)) return false;
        if (t.ValueKind != JsonValueKind.String) return false;
        if (t.GetString() != "tool_result") return false;
        if (!block.TryGetProperty("is_error", out var err)) return false;
        return err.ValueKind == JsonValueKind.True;
    }

    private static string FormatDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? "(no detail)"
            : StreamTextFormatting.Truncate(StreamTextFormatting.CollapseWhitespace(detail));

    private static string? ExtractToolResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content)) return null;
        if (content.ValueKind == JsonValueKind.String) return content.GetString();
        if (content.ValueKind != JsonValueKind.Array) return null;
        return TryGetFirstTextFromArray(content);
    }

    private static string? TryGetFirstTextFromArray(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("text", out var t)) continue;
            if (t.ValueKind != JsonValueKind.String) continue;
            return t.GetString();
        }
        return null;
    }
}
