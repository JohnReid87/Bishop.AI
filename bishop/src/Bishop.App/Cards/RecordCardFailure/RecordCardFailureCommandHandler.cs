using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RecordCardFailure;

internal sealed class RecordCardFailureCommandHandler : IRequestHandler<RecordCardFailureCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecordCardFailureCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RecordCardFailureCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {request.BatchId} not found.");

        card.TotalCostUsd += request.CostUsd;

        var now = _timeProvider.GetUtcNow();
        card.LastAutoRunFailedAt = now;
        batch.StoppedAt = now;

        await db.SaveChangesAsync(cancellationToken);
    }
}
