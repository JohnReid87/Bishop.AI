using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes.ListLanesByWorkspace;

public sealed class ListLanesByWorkspaceQueryHandler : IRequestHandler<ListLanesByWorkspaceQuery, IReadOnlyList<Lane>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListLanesByWorkspaceQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Lane>> Handle(ListLanesByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Lanes
            .AsNoTracking()
            .Where(l => l.WorkspaceId == request.WorkspaceId)
            .OrderBy(l => l.Position)
            .ToListAsync(cancellationToken);
    }
}
