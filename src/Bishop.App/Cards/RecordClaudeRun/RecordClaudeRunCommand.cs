using MediatR;

namespace Bishop.App.Cards.RecordClaudeRun;

public sealed record RecordClaudeRunCommand(
    Guid CardId,
    int InputTokens,
    int OutputTokens) : IRequest;
