using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Workspaces.UpdateWorkspace;

public sealed class UpdateWorkspaceCommandHandler : IRequestHandler<UpdateWorkspaceCommand, Workspace>
{
    private readonly BishopDbContext _db;

    public UpdateWorkspaceCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Workspace> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        workspace.Name = request.Name;
        workspace.Path = request.Path;
        await _db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
