using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Tags;

public sealed class DefaultTagSeeder : IDefaultTagSeeder
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public DefaultTagSeeder(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task EnsureAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        var fullPath = Path.GetFullPath(workspacePath);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspaces = await db.Workspaces
            .Include(w => w.Tags)
            .ToListAsync(cancellationToken);

        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(Path.GetFullPath(w.Path), fullPath, StringComparison.OrdinalIgnoreCase));
        if (workspace is null)
            return;

        if (ApplyBrandColours(db, workspace))
            await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var workspaces = await db.Workspaces
            .Include(w => w.Tags)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var workspace in workspaces)
            changed |= ApplyBrandColours(db, workspace);

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static bool ApplyBrandColours(BishopDbContext db, Workspace workspace)
    {
        var byName = workspace.Tags.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (name, colour) in BrandTagPalette.DefaultColours)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                if (!string.Equals(existing.Colour, colour, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Colour = colour;
                    changed = true;
                }
            }
            else
            {
                db.Tags.Add(new Tag
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    Name = name,
                    Colour = colour,
                });
                changed = true;
            }
        }

        return changed;
    }
}
