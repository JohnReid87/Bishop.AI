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

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var allWorkspaces = await db.Workspaces
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
            db.Workspaces.Add(workspace);

            for (var i = 0; i < SystemLaneNames.All.Count; i++)
            {
                db.Lanes.Add(new Lane
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    Name = SystemLaneNames.All[i],
                    Position = i + 1,
                    IsSystem = true,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            created = true;
            lanesAdded = SystemLaneNames.All;
        }
        else
        {
            workspace = existing;
            var existingLaneNames = existing.Lanes
                .Select(l => l.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var lanesAddedList = new List<string>();
            var changed = false;

            // Promote any user-created Backlog lane to system
            var userBacklog = existing.Lanes
                .FirstOrDefault(l => string.Equals(l.Name, SystemLaneNames.Backlog, StringComparison.OrdinalIgnoreCase) && !l.IsSystem);
            if (userBacklog is not null)
            {
                userBacklog.IsSystem = true;
                changed = true;
            }

            var maxPosition = existing.Lanes.Count > 0 ? existing.Lanes.Max(l => l.Position) : 0;

            // If Backlog is absent, insert at position 1 and shift all existing lanes up
            if (!existingLaneNames.Contains(SystemLaneNames.Backlog))
            {
                foreach (var lane in existing.Lanes)
                    lane.Position++;
                maxPosition++;

                db.Lanes.Add(new Lane
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = existing.Id,
                    Name = SystemLaneNames.Backlog,
                    Position = 1,
                    IsSystem = true,
                });
                lanesAddedList.Add(SystemLaneNames.Backlog);
                changed = true;
            }

            // Append any other missing system lanes at the end
            var otherMissing = SystemLaneNames.All
                .Where(n => !string.Equals(n, SystemLaneNames.Backlog, StringComparison.OrdinalIgnoreCase))
                .Where(n => !existingLaneNames.Contains(n))
                .ToList();

            if (otherMissing.Count > 0)
            {
                var nextPosition = maxPosition + 1;
                foreach (var laneName in otherMissing)
                {
                    db.Lanes.Add(new Lane
                    {
                        Id = Guid.NewGuid(),
                        WorkspaceId = existing.Id,
                        Name = laneName,
                        Position = nextPosition++,
                        IsSystem = true,
                    });
                    lanesAddedList.Add(laneName);
                }
                changed = true;
            }

            if (changed)
                await db.SaveChangesAsync(cancellationToken);

            created = false;
            lanesAdded = lanesAddedList;
        }

        var tagsAdded = await SeedTagsAsync(db, workspace, request.SeedTags, cancellationToken);
        var gitHubLinked = await DetectGitHubAsync(db, workspace, normalizedPath, request.DetectGitHub, cancellationToken);

        return new InitWorkspaceResult(workspace, created, lanesAdded, tagsAdded, gitHubLinked);
    }

    private static async Task<IReadOnlyList<string>> SeedTagsAsync(
        BishopDbContext db, Workspace workspace, bool seed, CancellationToken cancellationToken)
    {
        if (!seed)
            return [];

        var existingTagNames = workspace.Tags
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = TagNames.All.Where(n => !existingTagNames.Contains(n)).ToList();
        if (toAdd.Count == 0)
            return [];

        foreach (var tagName in toAdd)
        {
            db.Tags.Add(new Tag
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = tagName,
                Colour = BrandTagPalette.DefaultColours[tagName],
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return toAdd;
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
