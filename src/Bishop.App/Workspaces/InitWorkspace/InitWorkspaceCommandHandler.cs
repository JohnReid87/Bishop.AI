using Bishop.Core;
using Bishop.Data;
using Bishop.App.Git;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed class InitWorkspaceCommandHandler : IRequestHandler<InitWorkspaceCommand, InitWorkspaceResult>
{
    private static readonly string[] DefaultLaneNames = ["To Do", "Doing", "Done"];
    private static readonly string[] DefaultTagNames = ["feature", "bug", "chore", "docs", "arch", "test", "spike"];

    private readonly BishopDbContext _db;
    private readonly IGitCli _git;

    public InitWorkspaceCommandHandler(BishopDbContext db, IGitCli git)
    {
        _db = db;
        _git = git;
    }

    public async Task<InitWorkspaceResult> Handle(InitWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.GetFullPath(request.Path);

        var allWorkspaces = await _db.Workspaces
            .Include(w => w.Lanes)
            .Include(w => w.Tags)
            .ToListAsync(cancellationToken);

        Workspace workspace;
        bool created;
        IReadOnlyList<string> lanesAdded;

        var existing = allWorkspaces.FirstOrDefault(w =>
            string.Equals(Path.GetFullPath(w.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            var name = request.Name ?? new DirectoryInfo(normalizedPath).Name;
            workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = name,
                Path = normalizedPath,
                Position = allWorkspaces.Count + 1,
            };
            _db.Workspaces.Add(workspace);

            for (var i = 0; i < DefaultLaneNames.Length; i++)
            {
                _db.Lanes.Add(new Lane
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    Name = DefaultLaneNames[i],
                    Position = i + 1,
                    IsSystem = true,
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            created = true;
            lanesAdded = DefaultLaneNames;
        }
        else
        {
            workspace = existing;
            var existingLaneNames = existing.Lanes
                .Select(l => l.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = DefaultLaneNames.Where(n => !existingLaneNames.Contains(n)).ToList();
            if (missing.Count > 0)
            {
                var nextPosition = existing.Lanes.Count > 0
                    ? existing.Lanes.Max(l => l.Position) + 1
                    : 1;

                foreach (var laneName in missing)
                {
                    _db.Lanes.Add(new Lane
                    {
                        Id = Guid.NewGuid(),
                        WorkspaceId = existing.Id,
                        Name = laneName,
                        Position = nextPosition++,
                        IsSystem = true,
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            created = false;
            lanesAdded = missing;
        }

        var tagsAdded = await SeedTagsAsync(workspace, request.SeedTags, cancellationToken);
        var gitHubLinked = await DetectGitHubAsync(workspace, normalizedPath, request.DetectGitHub, cancellationToken);

        return new InitWorkspaceResult(workspace, created, lanesAdded, tagsAdded, gitHubLinked);
    }

    private async Task<IReadOnlyList<string>> SeedTagsAsync(
        Workspace workspace, bool seed, CancellationToken cancellationToken)
    {
        if (!seed)
            return [];

        var existingTagNames = workspace.Tags
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = DefaultTagNames.Where(n => !existingTagNames.Contains(n)).ToList();
        if (toAdd.Count == 0)
            return [];

        foreach (var tagName in toAdd)
        {
            _db.Tags.Add(new Tag
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = tagName,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return toAdd;
    }

    private async Task<bool> DetectGitHubAsync(
        Workspace workspace, string workspacePath, bool detect, CancellationToken cancellationToken)
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
        await _db.SaveChangesAsync(cancellationToken);
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
