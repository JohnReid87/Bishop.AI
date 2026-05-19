using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Workspaces.SetWorkspaceGitHubRepo;

public sealed class SetWorkspaceGitHubRepoCommandHandler : IRequestHandler<SetWorkspaceGitHubRepoCommand, Workspace>
{
    private readonly BishopDbContext _db;

    public SetWorkspaceGitHubRepoCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Workspace> Handle(SetWorkspaceGitHubRepoCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        var normalized = Normalize(request.Repo);
        if (normalized.Count(c => c == '/') != 1 || normalized.StartsWith('/') || normalized.EndsWith('/'))
            throw new InvalidOperationException($"Invalid GitHub repo '{request.Repo}': expected owner/repo format.");

        workspace.GitHubRepo = normalized;
        await _db.SaveChangesAsync(cancellationToken);
        return workspace;
    }

    private static string Normalize(string input)
    {
        var s = input.Trim();
        if (s.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            s = s["https://github.com/".Length..];
        else if (s.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            s = s["http://github.com/".Length..];
        else if (s.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            s = s["git@github.com:".Length..];
        s = s.TrimEnd('/');
        if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            s = s[..^4];
        return s;
    }
}
