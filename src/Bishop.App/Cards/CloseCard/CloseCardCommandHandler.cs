using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.CloseCard;

internal sealed class CloseCardCommandHandler : IRequestHandler<CloseCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public CloseCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Card> Handle(CloseCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = true;
        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}
