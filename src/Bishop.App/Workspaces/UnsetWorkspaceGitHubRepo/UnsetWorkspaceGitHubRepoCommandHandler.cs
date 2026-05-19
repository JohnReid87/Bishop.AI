using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;

public sealed class UnsetWorkspaceGitHubRepoCommandHandler : IRequestHandler<UnsetWorkspaceGitHubRepoCommand, Workspace>
{
    private readonly BishopDbContext _db;

    public UnsetWorkspaceGitHubRepoCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Workspace> Handle(UnsetWorkspaceGitHubRepoCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        workspace.GitHubRepo = null;
        await _db.SaveChangesAsync(cancellationToken);
        return workspace;
    }
}
