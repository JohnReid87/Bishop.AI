using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.RemoveWorkspace;

internal sealed class RemoveWorkspaceCommandHandler : IRequestHandler<RemoveWorkspaceCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RemoveWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RemoveWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.Id} not found.");

        workspace.IsRemoved = true;
        workspace.RemovedAt = _timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }
}
