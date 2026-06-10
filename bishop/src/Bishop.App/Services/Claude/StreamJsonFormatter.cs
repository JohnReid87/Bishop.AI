using System.Text.Json;

namespace Bishop.App.Services.Claude;

public sealed class StreamJsonFormatter
{
    public StreamJsonFormatter(Action<string>? onStatus = null, Action<PermissionDeniedEvent>? onDenial = null)
    {
        OnStatus = onStatus;
        OnDenial = onDenial;
    }

    internal Action<string>? OnStatus { get; }

    internal Action<PermissionDeniedEvent>? OnDenial { get; }

    public ClaudeRunTotals? Totals { get; internal set; }

    public int RunningInputTokens { get; internal set; }

    public int RunningOutputTokens { get; internal set; }

    public int RunningCacheCreationTokens { get; internal set; }

    public int RunningCacheReadTokens { get; internal set; }

    public int ToolUseCount { get; internal set; }

    public string? Format(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        if (!TryParse(line, out var doc))
            return null;

        using (doc)
        {
            return DispatchByType(doc.RootElement);
        }
    }

    private string? DispatchByType(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
            return null;

        return typeProp.GetString() switch
        {
            "assistant" => AssistantBlockFormatter.Format(root, this),
            "user" => UserBlockFormatter.Format(root),
            "result" => ResultBlockFormatter.Format(root, this),
            "system" => SystemBlockFormatter.Format(root, this),
            _ => null,
        };
    }

    private static bool TryParse(string line, out JsonDocument doc)
    {
        try
        {
            doc = JsonDocument.Parse(line);
            return true;
        }
        catch (JsonException)
        {
            doc = null!;
            return false;
        }
    }
}
