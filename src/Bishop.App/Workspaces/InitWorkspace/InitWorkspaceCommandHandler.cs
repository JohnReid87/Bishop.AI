using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.InitWorkspace;

internal sealed class InitWorkspaceCommandHandler : IRequestHandler<InitWorkspaceCommand, InitWorkspaceResult>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public InitWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<InitWorkspaceResult> Handle(InitWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.GetFullPath(request.Path);
        var normalizedPathLower = normalizedPath.ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Workspaces
            .FirstOrDefaultAsync(w => !w.IsRemoved && w.Path.ToLower() == normalizedPathLower, cancellationToken);

        if (existing is not null)
            return new InitWorkspaceResult(existing, Created: false);

        var archived = await db.Workspaces
            .FirstOrDefaultAsync(w => w.IsRemoved && w.Path.ToLower() == normalizedPathLower, cancellationToken);

        if (archived is not null)
        {
            if (request.ArchivedAction is null)
                return new InitWorkspaceResult(archived, Created: false, NeedsArchivedAction: true);

            if (request.ArchivedAction == InitWorkspaceArchivedAction.Restore)
            {
                archived.IsRemoved = false;
                archived.RemovedAt = null;
                await db.SaveChangesAsync(cancellationToken);
                return new InitWorkspaceResult(archived, Created: false, Restored: true);
            }

            // Fresh: purge the archived record, then fall through to create new
            db.Workspaces.Remove(archived);
            await db.SaveChangesAsync(cancellationToken);
        }

        var activeCount = await db.Workspaces.CountAsync(w => !w.IsRemoved, cancellationToken);
        var name = request.Name ?? new DirectoryInfo(normalizedPath).Name;
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = normalizedPath,
            Position = activeCount + 1,
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(cancellationToken);

        return new InitWorkspaceResult(workspace, Created: true);
    }
}
