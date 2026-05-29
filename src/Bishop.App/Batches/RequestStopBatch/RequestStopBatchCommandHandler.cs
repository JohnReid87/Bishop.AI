using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.RequestStopBatch;

public sealed class RequestStopBatchCommandHandler : IRequestHandler<RequestStopBatchCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RequestStopBatchCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(RequestStopBatchCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {request.BatchId} not found.");

        if (batch.Status != BatchStatus.Working)
            throw new InvalidOperationException(
                $"Cannot request stop on a batch that is not Working (current: {batch.Status}).");

        batch.StoppedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
