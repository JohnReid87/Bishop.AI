using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Workspaces.GetWorkspace;

public sealed class GetWorkspaceQueryHandler : IRequestHandler<GetWorkspaceQuery, Workspace?>
{
    private readonly BishopDbContext _db;

    public GetWorkspaceQueryHandler(BishopDbContext db) => _db = db;

    public async Task<Workspace?> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        return await _db.Workspaces.FindAsync([request.Id], cancellationToken);
    }
}
