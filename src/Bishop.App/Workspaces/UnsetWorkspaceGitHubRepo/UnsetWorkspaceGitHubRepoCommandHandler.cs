using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;

internal sealed class UnsetWorkspaceGitHubRepoCommandHandler : IRequestHandler<UnsetWorkspaceGitHubRepoCommand, Workspace>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public UnsetWorkspaceGitHubRepoCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Workspace> Handle(UnsetWorkspaceGitHubRepoCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        workspace.GitHubRepo = null;
        await db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
