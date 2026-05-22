using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.ReorderLanes;

public sealed class ReorderLanesCommandHandler : IRequestHandler<ReorderLanesCommand, Unit>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ReorderLanesCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Unit> Handle(ReorderLanesCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var lanes = await db.Lanes
            .Where(l => l.WorkspaceId == request.WorkspaceId)
            .ToListAsync(cancellationToken);

        var positionMap = request.OrderedIds
            .Select((id, index) => (id, position: index + 1))
            .ToDictionary(x => x.id, x => x.position);

        foreach (var lane in lanes)
        {
            if (positionMap.TryGetValue(lane.Id, out var position))
                lane.Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
