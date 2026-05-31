using MediatR;

namespace Bishop.App.Batches.AddCardToBatch;

public sealed record AddCardToBatchCommand(string BatchName, Guid CardId) : IRequest;
