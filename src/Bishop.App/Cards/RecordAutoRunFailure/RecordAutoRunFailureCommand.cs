using MediatR;

namespace Bishop.App.Cards.RecordAutoRunFailure;

public sealed record RecordAutoRunFailureCommand(Guid CardId) : IRequest;
