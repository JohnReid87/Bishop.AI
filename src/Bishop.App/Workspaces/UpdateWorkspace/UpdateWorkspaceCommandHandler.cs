using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.UpdateWorkspace;

public sealed class UpdateWorkspaceCommandHandler : IRequestHandler<UpdateWorkspaceCommand, Workspace>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public UpdateWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Workspace> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        workspace.Name = request.Name;
        workspace.Path = request.Path;
        await db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
