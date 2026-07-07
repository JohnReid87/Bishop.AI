using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.UnstarCard;

internal sealed class UnstarCardCommandHandler : IRequestHandler<UnstarCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public UnstarCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Card> Handle(UnstarCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsStarred = false;
        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}
