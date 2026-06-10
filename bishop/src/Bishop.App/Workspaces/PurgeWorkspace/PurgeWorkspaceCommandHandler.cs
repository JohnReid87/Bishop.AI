using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.PurgeWorkspace;

internal sealed class PurgeWorkspaceCommandHandler : IRequestHandler<PurgeWorkspaceCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public PurgeWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(PurgeWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        if (!workspace.IsRemoved)
            throw new InvalidOperationException(
                $"Workspace '{workspace.Name}' is active. Run 'bishop workspace remove' first.");

        db.Workspaces.Remove(workspace);
        await db.SaveChangesAsync(cancellationToken);
    }
}
