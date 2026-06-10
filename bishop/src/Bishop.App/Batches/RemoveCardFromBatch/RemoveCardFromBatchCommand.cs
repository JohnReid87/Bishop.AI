using MediatR;

namespace Bishop.App.Batches.RemoveCardFromBatch;

public sealed record RemoveCardFromBatchCommand(string BatchName, Guid CardId) : IRequest;
