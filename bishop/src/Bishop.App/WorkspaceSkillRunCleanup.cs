using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bishop.App;

// One-shot sweep that collapses legacy per-project rows for skills not in
// PerProjectSkills.Allowlist into a single (WorkspaceId, SkillName) row with
// ProjectName=null. Without it, workspaces audited before the allowlist landed
// would keep showing duplicate monitoring rows.
internal sealed class WorkspaceSkillRunCleanup : IHostedService
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public WorkspaceSkillRunCleanup(IDbContextFactory<BishopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var candidates = await db.WorkspaceSkillRuns
            .Where(r => r.ProjectName != null)
            .ToListAsync(cancellationToken);

        var collapsible = candidates
            .Where(r => !PerProjectSkills.IsPerProject(r.SkillName))
            .GroupBy(r => new { r.WorkspaceId, r.SkillName });

        var changed = false;
        foreach (var group in collapsible)
        {
            var ordered = group.OrderByDescending(r => r.RecordedAt).ToList();
            var keep = ordered[0];

            // If a generic (ProjectName=null) row already exists for this pair, drop
            // the per-project runs entirely and let the generic row stand.
            var generic = await db.WorkspaceSkillRuns
                .FirstOrDefaultAsync(
                    r => r.WorkspaceId == group.Key.WorkspaceId
                      && r.SkillName == group.Key.SkillName
                      && r.ProjectName == null,
                    cancellationToken);

            if (generic is null)
            {
                keep.ProjectName = null;
                db.WorkspaceSkillRuns.RemoveRange(ordered.Skip(1));
            }
            else
            {
                db.WorkspaceSkillRuns.RemoveRange(ordered);
            }
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
