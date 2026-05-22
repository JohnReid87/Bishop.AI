using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.MoveCard;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ClaimCard;

public sealed class ClaimCardCommandHandler : IRequestHandler<ClaimCardCommand, Card?>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;

    public ClaimCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, ISender sender)
    {
        _dbFactory = dbFactory;
        _sender = sender;
    }

    public async Task<Card?> Handle(ClaimCardCommand request, CancellationToken cancellationToken)
    {
        Guid topCardId;
        Guid doingLaneId;

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var lanes = await db.Lanes
                .AsNoTracking()
                .Where(l => l.WorkspaceId == request.WorkspaceId)
                .ToListAsync(cancellationToken);

            var sourceLane = lanes.FirstOrDefault(l =>
                string.Equals(l.Name, request.SourceLaneName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Lane '{request.SourceLaneName}' not found in workspace.");

            var doingLane = lanes.FirstOrDefault(l =>
                string.Equals(l.Name, SystemLaneNames.Doing, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    "Lane 'Doing' not found in workspace.");

            var query = db.Cards
                .AsNoTracking()
                .Where(c => c.LaneId == sourceLane.Id);

            if (!string.IsNullOrEmpty(request.TagName))
            {
                var tagName = request.TagName;
                query = query.Where(c => c.CardTags.Any(ct => ct.Tag.Name == tagName));
            }

            var topCard = await query
                .OrderBy(c => c.Position)
                .FirstOrDefaultAsync(cancellationToken);

            if (topCard is null)
                return null;

            topCardId = topCard.Id;
            doingLaneId = doingLane.Id;
        }

        await _sender.Send(new MoveCardCommand(topCardId, doingLaneId, 1), cancellationToken);

        return await _sender.Send(new GetCardQuery(topCardId), cancellationToken);
    }
}
