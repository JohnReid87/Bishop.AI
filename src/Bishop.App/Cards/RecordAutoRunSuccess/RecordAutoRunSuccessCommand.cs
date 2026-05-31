using MediatR;

namespace Bishop.App.Cards.RecordAutoRunSuccess;

public sealed record RecordAutoRunSuccessCommand(Guid CardId) : IRequest;
