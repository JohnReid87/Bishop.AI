using MediatR;

namespace Bishop.App.Batches.RequestStopBatch;

public sealed record RequestStopBatchCommand(Guid BatchId) : IRequest;
