using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Cards.CloseCard;

public sealed class CloseCardCommandHandler : IRequestHandler<CloseCardCommand, Card>
{
    private readonly BishopDbContext _db;

    public CloseCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Card> Handle(CloseCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards.FindAsync([request.CardId], cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = true;
        await _db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
