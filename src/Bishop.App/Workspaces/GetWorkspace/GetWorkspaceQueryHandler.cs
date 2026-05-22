using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.GetWorkspace;

public sealed class GetWorkspaceQueryHandler : IRequestHandler<GetWorkspaceQuery, Workspace?>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetWorkspaceQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Workspace?> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Workspaces
            .AsNoTracking()
            .Include(w => w.Lanes.OrderBy(l => l.Position))
            .Include(w => w.Tags.OrderBy(t => t.Name))
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);
    }
}
