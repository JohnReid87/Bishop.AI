using MediatR;

namespace Bishop.App.Cards.RecordCardFailure;

/// <summary>
/// Atomic post-failure mutations: stamp <c>LastAutoRunFailedAt</c> on the card
/// and <c>StoppedAt</c> on the batch under one <c>SaveChangesAsync</c>.
/// </summary>
public sealed record RecordCardFailureCommand(Guid CardId, Guid BatchId) : IRequest;
