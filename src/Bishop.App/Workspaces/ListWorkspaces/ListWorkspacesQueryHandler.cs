using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.ListWorkspaces;

public sealed class ListWorkspacesQueryHandler : IRequestHandler<ListWorkspacesQuery, IReadOnlyList<Workspace>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListWorkspacesQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Workspace>> Handle(ListWorkspacesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Workspaces
            .AsNoTracking()
            .Where(w => !w.IsRemoved)
            .OrderBy(w => w.Position)
            .ToListAsync(cancellationToken);
    }
}
