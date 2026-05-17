using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.ListWorkspaces;

public sealed class ListWorkspacesQueryHandler : IRequestHandler<ListWorkspacesQuery, IReadOnlyList<Workspace>>
{
    private readonly BishopDbContext _db;

    public ListWorkspacesQueryHandler(BishopDbContext db) => _db = db;

    public async Task<IReadOnlyList<Workspace>> Handle(ListWorkspacesQuery request, CancellationToken cancellationToken)
    {
        return await _db.Workspaces
            .OrderBy(w => w.Position)
            .ToListAsync(cancellationToken);
    }
}
