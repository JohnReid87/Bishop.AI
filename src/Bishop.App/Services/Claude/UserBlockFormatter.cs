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

        var lines = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            var line = FormatErrorBlock(block);
            if (line is not null) lines.Add(line);
        }
        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string? FormatErrorBlock(JsonElement block)
    {
        if (block.ValueKind != JsonValueKind.Object) return null;
        if (!block.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) return null;
        if (t.GetString() != "tool_result") return null;
        if (!block.TryGetProperty("is_error", out var err) || err.ValueKind != JsonValueKind.True)
            return null;

        var detail = ExtractToolResultText(block);
        var snippet = string.IsNullOrWhiteSpace(detail)
            ? "(no detail)"
            : StreamTextFormatting.Truncate(StreamTextFormatting.CollapseWhitespace(detail));
        return $"[error] {snippet}";
    }

    private static string? ExtractToolResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content)) return null;
        if (content.ValueKind == JsonValueKind.String) return content.GetString();
        if (content.ValueKind != JsonValueKind.Array) return null;

        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString();
        }
        return null;
    }
}
