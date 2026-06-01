using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.DeleteWorkspace;

internal sealed class DeleteWorkspaceCommandHandler : IRequestHandler<DeleteWorkspaceCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public DeleteWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(DeleteWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        if (!workspace.IsRemoved)
            throw new InvalidOperationException(
                $"Workspace '{workspace.Name}' is active. Call RemoveWorkspace first.");

        db.Workspaces.Remove(workspace);
        await db.SaveChangesAsync(cancellationToken);
    }
}
