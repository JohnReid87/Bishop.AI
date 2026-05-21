using MediatR;

namespace Bishop.App.Cards.RecordClaudeRun;

public sealed record RecordClaudeRunCommand(
    Guid CardId,
    decimal CostUsd,
    int InputTokens,
    int OutputTokens) : IRequest;
