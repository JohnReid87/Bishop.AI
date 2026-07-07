using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.StarCard;

internal sealed class StarCardCommandHandler : IRequestHandler<StarCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public StarCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Card> Handle(StarCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsStarred = true;
        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}
