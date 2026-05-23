using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.MoveCard;

public sealed class MoveCardCommandHandler : IRequestHandler<MoveCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;

    public MoveCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, ISender sender)
    {
        _dbFactory = dbFactory;
        _sender = sender;
    }

    public async Task<Card> Handle(MoveCardCommand request, CancellationToken cancellationToken)
    {
        Card card;
        bool enteringDone;
        bool leavingDone;

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            card = await db.Cards.FindAsync([request.CardId], cancellationToken)
                ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

            var targetLane = await db.Lanes.FindAsync([request.ToLaneId], cancellationToken)
                ?? throw new InvalidOperationException($"Lane {request.ToLaneId} not found.");

            if (request.ExpectedSourceLaneId is { } expectedLaneId)
            {
                var expectedLane = await db.Lanes.FindAsync([expectedLaneId], cancellationToken);
                if (expectedLane is null || card.LaneName != expectedLane.Name || card.WorkspaceId != expectedLane.WorkspaceId)
                    throw new InvalidOperationException(
                        $"Card {request.CardId} was expected in lane {expectedLaneId} but is in lane '{card.LaneName}'.");
            }

            var sourceLaneName = card.LaneName;
            var workspaceId = card.WorkspaceId;
            var movingAcrossLanes = sourceLaneName != targetLane.Name || workspaceId != targetLane.WorkspaceId;

            enteringDone = false;
            leavingDone = false;

            if (movingAcrossLanes)
            {
                var sourceDone = sourceLaneName == SystemLaneNames.Done;
                var targetDone = targetLane.Name == SystemLaneNames.Done;
                enteringDone = targetDone && !sourceDone;
                leavingDone = sourceDone && !targetDone;

                // Renumber remaining cards in the source lane.
                var sourceCards = await db.Cards
                    .Where(c => c.WorkspaceId == workspaceId && c.LaneName == sourceLaneName && c.Id != card.Id)
                    .OrderBy(c => c.Position)
                    .ToListAsync(cancellationToken);
                for (var i = 0; i < sourceCards.Count; i++)
                    sourceCards[i].Position = i + 1;
            }

            // Load target lane cards (excluding the card being moved if same lane).
            var targetCards = await db.Cards
                .Where(c => c.WorkspaceId == targetLane.WorkspaceId && c.LaneName == targetLane.Name && c.Id != card.Id)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);

            // Insert at the requested position (1-based, clamped to valid range).
            var insertAt = Math.Clamp(request.ToPosition - 1, 0, targetCards.Count);
            targetCards.Insert(insertAt, card);

            card.LaneName = targetLane.Name;
            card.WorkspaceId = targetLane.WorkspaceId;
            for (var i = 0; i < targetCards.Count; i++)
                targetCards[i].Position = i + 1;

            await db.SaveChangesAsync(cancellationToken);
        }

        if (enteringDone && !request.KeepOpen)
            await _sender.Send(new CloseCardCommand(card.Id), cancellationToken);
        else if (leavingDone)
            await _sender.Send(new ReopenCardCommand(card.Id), cancellationToken);

        return card;
    }
}
