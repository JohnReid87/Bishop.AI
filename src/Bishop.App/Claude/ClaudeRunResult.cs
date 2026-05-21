namespace Bishop.App.Claude;

public sealed record ClaudeRunResult(int ExitCode, ClaudeRunTotals? Totals, int ToolUseCount = 0);
