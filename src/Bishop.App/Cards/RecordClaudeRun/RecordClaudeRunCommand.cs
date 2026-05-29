using MediatR;

namespace Bishop.App.Cards.RecordClaudeRun;

public sealed record RecordClaudeRunCommand(
    Guid CardId,
    int InputTokens,
    int OutputTokens,
    int CacheCreationTokens = 0,
    int CacheReadTokens = 0,
    decimal CostUsd = 0m) : IRequest;
