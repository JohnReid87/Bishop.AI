using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.ListLanesByWorkspace;

public sealed class ListLanesByWorkspaceQueryHandler : IRequestHandler<ListLanesByWorkspaceQuery, IReadOnlyList<Lane>>
{
    private readonly BishopDbContext _db;

    public ListLanesByWorkspaceQueryHandler(BishopDbContext db) => _db = db;

    public async Task<IReadOnlyList<Lane>> Handle(ListLanesByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        return await _db.Lanes
            .Where(l => l.WorkspaceId == request.WorkspaceId)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);
    }
}
