using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.ListWorkspaces;

internal sealed class ListWorkspacesQueryHandler : IRequestHandler<ListWorkspacesQuery, IReadOnlyList<Workspace>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListWorkspacesQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Workspace>> Handle(ListWorkspacesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Workspaces.AsNoTracking();
        if (!request.IncludeRemoved)
            query = query.Where(w => !w.IsRemoved);
        if (!request.IncludeHidden)
            query = query.Where(w => !w.IsHidden);
        return await query
            .OrderBy(w => w.Position)
            .ToListAsync(cancellationToken);
    }
}
