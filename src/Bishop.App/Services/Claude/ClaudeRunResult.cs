namespace Bishop.App.Services.Claude;

public sealed record ClaudeRunResult(int ExitCode, ClaudeRunTotals? Totals, int ToolUseCount = 0, string? TranscriptPath = null);
