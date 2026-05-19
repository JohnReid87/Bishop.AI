using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Cards.ReopenCard;

public sealed class ReopenCardCommandHandler : IRequestHandler<ReopenCardCommand, Card>
{
    private readonly BishopDbContext _db;

    public ReopenCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Card> Handle(ReopenCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards.FindAsync([request.CardId], cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = false;
        await _db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
