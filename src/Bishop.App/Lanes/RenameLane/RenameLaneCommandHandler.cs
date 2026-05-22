using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.RenameLane;

public sealed class RenameLaneCommandHandler : IRequestHandler<RenameLaneCommand, Lane>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RenameLaneCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Lane> Handle(RenameLaneCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var lane = await db.Lanes.FindAsync([request.LaneId], cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneId} not found.");

        if (lane.IsSystem)
            throw new InvalidOperationException(
                $"Lane '{lane.Name}' is a system lane and cannot be renamed.");

        lane.Name = request.NewName;
        await db.SaveChangesAsync(cancellationToken);
        return lane;
    }
}
