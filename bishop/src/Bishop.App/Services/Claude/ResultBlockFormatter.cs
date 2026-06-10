using System.Text.Json;

namespace Bishop.App.Services.Claude;

internal static class ResultBlockFormatter
{
    public static string? Format(JsonElement root, StreamJsonFormatter state)
    {
        var duration = ReadDuration(root);
        var costUsd = JsonReadHelpers.ReadDecimal(root, "total_cost_usd");

        state.Totals = ComputeTotals(root, state, costUsd);

        var summary = BuildBaseSummary(duration, state.ToolUseCount);
        var tokenSegment = RunFormatting.FormatFinalTokenSegment(state.Totals);
        return tokenSegment is null ? summary : $"{summary} · {tokenSegment}";
    }

    private static string? ReadDuration(JsonElement root)
    {
        if (root.TryGetProperty("duration_ms", out var durProp)
            && durProp.ValueKind == JsonValueKind.Number
            && durProp.TryGetInt64(out var durMs))
        {
            return RunFormatting.FormatDuration(TimeSpan.FromMilliseconds(durMs));
        }
        return null;
    }

    private static ClaudeRunTotals? ComputeTotals(JsonElement root, StreamJsonFormatter state, decimal costUsd)
    {
        if (TryReadModelUsageTotals(root, out var modelTotals))
            return modelTotals! with { CostUsd = costUsd };
        if (state.RunningInputTokens > 0 || state.RunningOutputTokens > 0)
            return new ClaudeRunTotals(
                state.RunningInputTokens,
                state.RunningOutputTokens,
                state.RunningCacheCreationTokens,
                state.RunningCacheReadTokens,
                costUsd);
        return null;
    }

    private static string BuildBaseSummary(string? duration, int toolUseCount)
    {
        var head = duration is null ? "done" : $"done in {duration}";
        var tail = $"{toolUseCount} tool {(toolUseCount == 1 ? "use" : "uses")}";
        return $"{head}, {tail}";
    }

    private static bool TryReadModelUsageTotals(JsonElement root, out ClaudeRunTotals? totals)
    {
        totals = null;
        if (!root.TryGetProperty("modelUsage", out var modelUsage) || modelUsage.ValueKind != JsonValueKind.Object)
            return false;

        int inputTokens = 0, outputTokens = 0, cacheCreation = 0, cacheRead = 0;
        var hasAny = false;
        foreach (var model in modelUsage.EnumerateObject())
        {
            if (model.Value.ValueKind != JsonValueKind.Object) continue;
            inputTokens += JsonReadHelpers.ReadInt(model.Value, "inputTokens");
            outputTokens += JsonReadHelpers.ReadInt(model.Value, "outputTokens");
            cacheCreation += JsonReadHelpers.ReadInt(model.Value, "cacheCreationInputTokens");
            cacheRead += JsonReadHelpers.ReadInt(model.Value, "cacheReadInputTokens");
            hasAny = true;
        }

        if (!hasAny) return false;
        totals = new ClaudeRunTotals(inputTokens, outputTokens, cacheCreation, cacheRead);
        return true;
    }
}
