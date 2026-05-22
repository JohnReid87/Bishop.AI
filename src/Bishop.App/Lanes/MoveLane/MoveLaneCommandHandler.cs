using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.MoveLane;

public sealed class MoveLaneCommandHandler : IRequestHandler<MoveLaneCommand, Lane>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public MoveLaneCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Lane> Handle(MoveLaneCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var lane = await db.Lanes.FindAsync([request.LaneId], cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneId} not found.");

        var otherLanes = await db.Lanes
            .Where(l => l.WorkspaceId == lane.WorkspaceId && l.Id != lane.Id)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);

        var insertAt = Math.Clamp(request.ToPosition - 1, 0, otherLanes.Count);
        otherLanes.Insert(insertAt, lane);

        for (var i = 0; i < otherLanes.Count; i++)
            otherLanes[i].Position = i + 1;

        await db.SaveChangesAsync(cancellationToken);
        return lane;
    }
}
