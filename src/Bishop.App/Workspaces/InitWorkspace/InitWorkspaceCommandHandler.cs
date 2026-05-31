using Bishop.Core;
using Bishop.Data;
using Bishop.App.Git;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed class InitWorkspaceCommandHandler : IRequestHandler<InitWorkspaceCommand, InitWorkspaceResult>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGitCli _git;

    public InitWorkspaceCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, IGitCli git)
    {
        _dbFactory = dbFactory;
        _git = git;
    }

    public async Task<InitWorkspaceResult> Handle(InitWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.GetFullPath(request.Path);
        var normalizedPathLower = normalizedPath.ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Workspaces
            .FirstOrDefaultAsync(w => !w.IsRemoved && w.Path.ToLower() == normalizedPathLower, cancellationToken);

        if (existing is not null)
        {
            var gitHubLinkedExisting = await DetectGitHubAsync(db, existing, normalizedPath, request.DetectGitHub, cancellationToken);
            return new InitWorkspaceResult(existing, Created: false, gitHubLinkedExisting);
        }

        var archived = await db.Workspaces
            .FirstOrDefaultAsync(w => w.IsRemoved && w.Path.ToLower() == normalizedPathLower, cancellationToken);

        if (archived is not null)
        {
            if (request.ArchivedAction is null)
                return new InitWorkspaceResult(archived, Created: false, GitHubLinked: false, NeedsArchivedAction: true);

            if (request.ArchivedAction == InitWorkspaceArchivedAction.Restore)
            {
                archived.IsRemoved = false;
                archived.RemovedAt = null;
                await db.SaveChangesAsync(cancellationToken);
                var gitHubLinkedRestore = await DetectGitHubAsync(db, archived, normalizedPath, request.DetectGitHub, cancellationToken);
                return new InitWorkspaceResult(archived, Created: false, gitHubLinkedRestore, Restored: true);
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

        var gitHubLinked = await DetectGitHubAsync(db, workspace, normalizedPath, request.DetectGitHub, cancellationToken);
        return new InitWorkspaceResult(workspace, Created: true, gitHubLinked);
    }

    private async Task<bool> DetectGitHubAsync(
        BishopDbContext db, Workspace workspace, string workspacePath, bool detect, CancellationToken cancellationToken)
    {
        if (!detect || !string.IsNullOrWhiteSpace(workspace.GitHubRepo))
            return false;

        var originUrl = await _git.GetOriginUrlAsync(workspacePath, cancellationToken);
        if (originUrl is null)
            return false;

        var slug = ParseGitHubSlug(originUrl);
        if (slug is null)
            return false;

        workspace.GitHubRepo = slug;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? ParseGitHubSlug(string url)
    {
        var s = url.Trim();

        if (s.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            s = s["https://github.com/".Length..];
        else if (s.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            s = s["http://github.com/".Length..];
        else if (s.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            s = s["git@github.com:".Length..];
        else
            return null;

        s = s.TrimEnd('/');
        if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            s = s[..^4];

        if (s.Count(c => c == '/') != 1 || s.StartsWith('/') || s.EndsWith('/'))
            return null;

        return s;
    }
}
