using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.RemoveWorkspace;

public sealed class RemoveWorkspaceCommandHandler : IRequestHandler<RemoveWorkspaceCommand, Unit>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RemoveWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Unit> Handle(RemoveWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        workspace.IsRemoved = true;
        workspace.RemovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
