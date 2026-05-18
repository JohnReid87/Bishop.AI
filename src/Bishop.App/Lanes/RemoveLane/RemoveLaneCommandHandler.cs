using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.RemoveLane;

public sealed class RemoveLaneCommandHandler : IRequestHandler<RemoveLaneCommand, Unit>
{
    private readonly BishopDbContext _db;

    public RemoveLaneCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Unit> Handle(RemoveLaneCommand request, CancellationToken cancellationToken)
    {
        var lane = await _db.Lanes.FindAsync([request.LaneId], cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneId} not found.");

        var cardCount = await _db.Cards.CountAsync(c => c.LaneId == lane.Id, cancellationToken);
        if (cardCount > 0)
            throw new InvalidOperationException(
                $"Lane '{lane.Name}' is not empty ({cardCount} card(s)). Move or remove its cards first.");

        var remainingLanes = await _db.Lanes
            .Where(l => l.WorkspaceId == lane.WorkspaceId && l.Id != lane.Id)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);

        _db.Lanes.Remove(lane);

        for (var i = 0; i < remainingLanes.Count; i++)
            remainingLanes[i].Position = i + 1;

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
