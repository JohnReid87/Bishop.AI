using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Batches.RequestStopBatch;

public sealed class RequestStopBatchCommandHandler : IRequestHandler<RequestStopBatchCommand>
{
    private readonly IBatchRepository _batches;

    public RequestStopBatchCommandHandler(IBatchRepository batches) => _batches = batches;

    public async Task Handle(RequestStopBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await _batches.GetAsync(request.BatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {request.BatchId} not found.");
        if (batch.Status != BatchStatus.Working)
            throw new InvalidOperationException(
                $"Cannot request stop on a batch that is not Working (current: {batch.Status}).");
        await _batches.SetStoppedAtAsync(batch.Id, DateTimeOffset.UtcNow, cancellationToken);
    }
}
