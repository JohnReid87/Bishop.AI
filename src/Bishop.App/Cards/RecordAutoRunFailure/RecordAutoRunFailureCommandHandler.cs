using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RecordAutoRunFailure;

public sealed class RecordAutoRunFailureCommandHandler : IRequestHandler<RecordAutoRunFailureCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecordAutoRunFailureCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RecordAutoRunFailureCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.LastAutoRunFailedAt = _timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
    }
}
