using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ReopenCard;

internal sealed class ReopenCardCommandHandler : IRequestHandler<ReopenCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ReopenCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Card> Handle(ReopenCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = false;
        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}
