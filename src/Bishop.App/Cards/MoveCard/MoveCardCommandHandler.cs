using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.MoveCard;

public sealed class MoveCardCommandHandler : IRequestHandler<MoveCardCommand, Card>
{
    private readonly BishopDbContext _db;

    public MoveCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Card> Handle(MoveCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards.FindAsync([request.CardId], cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        var sourceLaneId = card.LaneId;
        var movingAcrossLanes = sourceLaneId != request.ToLaneId;

        if (movingAcrossLanes)
        {
            // Renumber remaining cards in the source lane.
            var sourceCards = await _db.Cards
                .Where(c => c.LaneId == sourceLaneId && c.Id != card.Id)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);
            for (var i = 0; i < sourceCards.Count; i++)
                sourceCards[i].Position = i + 1;
        }

        // Load target lane cards (excluding the card being moved if same lane).
        var targetCards = await _db.Cards
            .Where(c => c.LaneId == request.ToLaneId && c.Id != card.Id)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        // Insert at the requested position (1-based, clamped to valid range).
        var insertAt = Math.Clamp(request.ToPosition - 1, 0, targetCards.Count);
        targetCards.Insert(insertAt, card);

        card.LaneId = request.ToLaneId;
        for (var i = 0; i < targetCards.Count; i++)
            targetCards[i].Position = i + 1;

        await _db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
