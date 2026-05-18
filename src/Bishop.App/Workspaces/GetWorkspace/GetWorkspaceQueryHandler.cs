using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.GetWorkspace;

public sealed class GetWorkspaceQueryHandler : IRequestHandler<GetWorkspaceQuery, Workspace?>
{
    private readonly BishopDbContext _db;

    public GetWorkspaceQueryHandler(BishopDbContext db) => _db = db;

    public async Task<Workspace?> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        return await _db.Workspaces
            .Include(w => w.Lanes.OrderBy(l => l.Position))
            .Include(w => w.Tags.OrderBy(t => t.Name))
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);
    }
}
