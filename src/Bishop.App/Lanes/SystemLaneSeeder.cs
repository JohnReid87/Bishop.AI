using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Lanes;

public sealed class SystemLaneSeeder : ISystemLaneSeeder
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public SystemLaneSeeder(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task EnsureAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        var fullPath = Path.GetFullPath(workspacePath);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspaces = await db.Workspaces
            .Include(w => w.Lanes)
            .ToListAsync(cancellationToken);
        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(Path.GetFullPath(w.Path), fullPath, StringComparison.OrdinalIgnoreCase));

        if (workspace is null)
            return;

        if (Apply(db, workspace))
            await db.SaveChangesAsync(cancellationToken);
    }

    internal static bool Apply(BishopDbContext db, Workspace workspace)
    {
        var existingLaneNames = workspace.Lanes
            .Select(l => l.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var changed = false;

        var userBacklog = workspace.Lanes
            .FirstOrDefault(l =>
                string.Equals(l.Name, SystemLaneNames.Backlog, StringComparison.OrdinalIgnoreCase)
                && !l.IsSystem);
        if (userBacklog is not null)
        {
            userBacklog.IsSystem = true;
            changed = true;
        }

        var maxPosition = workspace.Lanes.Count > 0 ? workspace.Lanes.Max(l => l.Position) : 0;

        if (!existingLaneNames.Contains(SystemLaneNames.Backlog))
        {
            foreach (var lane in workspace.Lanes)
                lane.Position++;
            maxPosition++;

            db.Lanes.Add(new Lane
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = SystemLaneNames.Backlog,
                Position = 1,
                IsSystem = true,
            });
            changed = true;
        }

        var otherMissing = SystemLaneNames.All
            .Where(n => !string.Equals(n, SystemLaneNames.Backlog, StringComparison.OrdinalIgnoreCase))
            .Where(n => !existingLaneNames.Contains(n))
            .ToList();

        foreach (var laneName in otherMissing)
        {
            db.Lanes.Add(new Lane
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = laneName,
                Position = ++maxPosition,
                IsSystem = true,
            });
            changed = true;
        }

        return changed;
    }
}
