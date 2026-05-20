using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.ReorderLanes;

public sealed class ReorderLanesCommandHandler : IRequestHandler<ReorderLanesCommand, Unit>
{
    private readonly BishopDbContext _db;

    public ReorderLanesCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Unit> Handle(ReorderLanesCommand request, CancellationToken cancellationToken)
    {
        var lanes = await _db.Lanes
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

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
