using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Lanes.RenameLane;

public sealed class RenameLaneCommandHandler : IRequestHandler<RenameLaneCommand, Lane>
{
    private readonly BishopDbContext _db;

    public RenameLaneCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Lane> Handle(RenameLaneCommand request, CancellationToken cancellationToken)
    {
        var lane = await _db.Lanes.FindAsync([request.LaneId], cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneId} not found.");

        if (lane.IsSystem)
            throw new InvalidOperationException(
                $"Lane '{lane.Name}' is a system lane and cannot be renamed.");

        lane.Name = request.NewName;
        await _db.SaveChangesAsync(cancellationToken);
        return lane;
    }
}
