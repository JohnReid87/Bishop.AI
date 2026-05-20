using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.MoveCard;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ClaimCard;

public sealed class ClaimCardCommandHandler : IRequestHandler<ClaimCardCommand, Card?>
{
    private readonly BishopDbContext _db;
    private readonly ISender _sender;

    public ClaimCardCommandHandler(BishopDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Card?> Handle(ClaimCardCommand request, CancellationToken cancellationToken)
    {
        var lanes = await _db.Lanes
            .AsNoTracking()
            .Where(l => l.WorkspaceId == request.WorkspaceId)
            .ToListAsync(cancellationToken);

        var sourceLane = lanes.FirstOrDefault(l =>
            string.Equals(l.Name, request.SourceLaneName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Lane '{request.SourceLaneName}' not found in workspace.");

        var doingLane = lanes.FirstOrDefault(l =>
            string.Equals(l.Name, "Doing", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Lane 'Doing' not found in workspace.");

        var query = _db.Cards
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

        await _sender.Send(new MoveCardCommand(topCard.Id, doingLane.Id, 1), cancellationToken);

        return await _sender.Send(new GetCardQuery(topCard.Id), cancellationToken);
    }
}
