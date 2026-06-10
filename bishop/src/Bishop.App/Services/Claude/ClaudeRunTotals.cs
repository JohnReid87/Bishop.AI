namespace Bishop.App.Services.Claude;

public sealed record ClaudeRunTotals(int InputTokens, int OutputTokens, int CacheCreationTokens = 0, int CacheReadTokens = 0, decimal CostUsd = 0m);
