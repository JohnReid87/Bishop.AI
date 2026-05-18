using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.AddLane;

public sealed class AddLaneCommandHandler : IRequestHandler<AddLaneCommand, Lane>
{
    private readonly BishopDbContext _db;

    public AddLaneCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Lane> Handle(AddLaneCommand request, CancellationToken cancellationToken)
    {
        var position = await _db.Lanes.CountAsync(l => l.WorkspaceId == request.WorkspaceId, cancellationToken) + 1;
        var lane = new Lane
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            Position = position,
        };
        _db.Lanes.Add(lane);
        await _db.SaveChangesAsync(cancellationToken);
        return lane;
    }
}
