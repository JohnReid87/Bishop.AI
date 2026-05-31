using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RecordAutoRunSuccess;

public sealed class RecordAutoRunSuccessCommandHandler : IRequestHandler<RecordAutoRunSuccessCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecordAutoRunSuccessCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RecordAutoRunSuccessCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.LastAutoRunSucceededAt = _timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
    }
}
