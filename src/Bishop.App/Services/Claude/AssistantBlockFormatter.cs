using System.Text.Json;

namespace Bishop.App.Services.Claude;

internal static class AssistantBlockFormatter
{
    public static string? Format(JsonElement root, StreamJsonFormatter state)
    {
        if (!JsonReadHelpers.TryGetMessage(root, out var message))
            return null;
        if (!JsonReadHelpers.TryGetContentArray(message, out var content))
            return null;

        AccumulateAssistantUsage(message, state);

        var lines = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;
            if (!block.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) continue;
            HandleBlock(block, t.GetString(), state, lines);
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static void HandleBlock(JsonElement block, string? blockType, StreamJsonFormatter state, List<string> lines)
    {
        switch (blockType)
        {
            case "text":
                HandleTextBlock(block, state, lines);
                break;
            case "tool_use":
                HandleToolUseBlock(block, state);
                break;
        }
    }

    private static void HandleTextBlock(JsonElement block, StreamJsonFormatter state, List<string> lines)
    {
        var cleaned = ExtractAssistantText(block);
        if (string.IsNullOrEmpty(cleaned)) return;
        EmitStatus(state, cleaned);
        lines.Add($"… {cleaned}");
    }

    private static void HandleToolUseBlock(JsonElement block, StreamJsonFormatter state)
    {
        state.ToolUseCount += 1;
        if (state.OnStatus is null) return;
        var toolName = ReadToolName(block);
        if (!string.IsNullOrEmpty(toolName))
            EmitStatus(state, $"Tool: {toolName}");
    }

    private static void AccumulateAssistantUsage(JsonElement message, StreamJsonFormatter state)
    {
        if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return;
        state.RunningInputTokens += JsonReadHelpers.ReadInt(usage, "input_tokens");
        state.RunningOutputTokens += JsonReadHelpers.ReadInt(usage, "output_tokens");
        state.RunningCacheCreationTokens += JsonReadHelpers.ReadInt(usage, "cache_creation_input_tokens");
        state.RunningCacheReadTokens += JsonReadHelpers.ReadInt(usage, "cache_read_input_tokens");
    }

    private static void EmitStatus(StreamJsonFormatter state, string label)
    {
        if (state.OnStatus is null) return;
        var suffix = RunFormatting.FormatTokenSuffix(
            state.RunningInputTokens,
            state.RunningOutputTokens,
            state.RunningCacheCreationTokens + state.RunningCacheReadTokens);
        state.OnStatus.Invoke(suffix is null ? label : $"{label} — {suffix}");
    }

    private static string? ExtractAssistantText(JsonElement block)
    {
        if (!block.TryGetProperty("text", out var textProp)
            || textProp.ValueKind != JsonValueKind.String)
            return null;
        var raw = textProp.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return StreamTextFormatting.Truncate(StreamTextFormatting.CollapseWhitespace(raw));
    }

    private static string? ReadToolName(JsonElement block)
    {
        if (block.TryGetProperty("name", out var nameProp)
            && nameProp.ValueKind == JsonValueKind.String)
            return nameProp.GetString();
        return null;
    }
}
