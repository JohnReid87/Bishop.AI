using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.RemoveLane;

public sealed class RemoveLaneCommandHandler : IRequestHandler<RemoveLaneCommand, Unit>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RemoveLaneCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Unit> Handle(RemoveLaneCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var lane = await db.Lanes.FindAsync([request.LaneId], cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneId} not found.");

        if (lane.IsSystem)
            throw new InvalidOperationException(
                $"Lane '{lane.Name}' is a system lane and cannot be deleted.");

        var cardCount = await db.Cards.CountAsync(c => c.LaneId == lane.Id, cancellationToken);
        if (cardCount > 0)
            throw new InvalidOperationException(
                $"Lane '{lane.Name}' is not empty ({cardCount} card(s)). Move or remove its cards first.");

        var remainingLanes = await db.Lanes
            .Where(l => l.WorkspaceId == lane.WorkspaceId && l.Id != lane.Id)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);

        db.Lanes.Remove(lane);

        for (var i = 0; i < remainingLanes.Count; i++)
            remainingLanes[i].Position = i + 1;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
