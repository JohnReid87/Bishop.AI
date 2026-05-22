using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.AddLane;

public sealed class AddLaneCommandHandler : IRequestHandler<AddLaneCommand, Lane>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AddLaneCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Lane> Handle(AddLaneCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var position = await db.Lanes.CountAsync(l => l.WorkspaceId == request.WorkspaceId, cancellationToken) + 1;
        var lane = new Lane
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            Position = position,
        };
        db.Lanes.Add(lane);
        await db.SaveChangesAsync(cancellationToken);
        return lane;
    }
}
