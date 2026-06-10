using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.MoveCard;

internal sealed class MoveCardCommandHandler : IRequestHandler<MoveCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public MoveCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Card> Handle(MoveCardCommand request, CancellationToken cancellationToken)
    {
        if (!SystemLaneNames.All.Contains(request.ToLaneName))
            throw new InvalidOperationException($"Lane '{request.ToLaneName}' is not a system lane.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var card = await db.Cards
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        if (request.ExpectedSourceLaneName is { } expectedLaneName)
        {
            if (card.LaneName != expectedLaneName)
                throw new InvalidOperationException(
                    $"Card {request.CardId} was expected in lane '{expectedLaneName}' but is in lane '{card.LaneName}'.");
        }

        var sourceLaneName = card.LaneName;
        var workspaceId = card.WorkspaceId;
        var movingAcrossLanes = sourceLaneName != request.ToLaneName;

        var enteringDone = false;
        var leavingDone = false;

        if (movingAcrossLanes)
        {
            var sourceDone = sourceLaneName == SystemLaneNames.Done;
            var targetDone = request.ToLaneName == SystemLaneNames.Done;
            enteringDone = targetDone && !sourceDone;
            leavingDone = sourceDone && !targetDone;

            var sourceCards = await db.Cards
                .Where(c => c.WorkspaceId == workspaceId && c.LaneName == sourceLaneName && c.Id != card.Id)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);
            for (var i = 0; i < sourceCards.Count; i++)
                sourceCards[i].Position = i + 1;
        }

        var targetCards = await db.Cards
            .Where(c => c.WorkspaceId == workspaceId && c.LaneName == request.ToLaneName && c.Id != card.Id)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        var insertAt = Math.Clamp(request.ToPosition - 1, 0, targetCards.Count);
        targetCards.Insert(insertAt, card);

        card.LaneName = request.ToLaneName;
        for (var i = 0; i < targetCards.Count; i++)
            targetCards[i].Position = i + 1;

        if (enteringDone)
            card.IsClosed = true;
        else if (leavingDone)
            card.IsClosed = false;

        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}
