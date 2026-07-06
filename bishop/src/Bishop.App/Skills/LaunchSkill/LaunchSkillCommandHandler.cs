using Bishop.App.Services.Terminal;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Skills.LaunchSkill;

internal sealed class LaunchSkillCommandHandler : IRequestHandler<LaunchSkillCommand, bool>
{
    private readonly ITerminalLauncher _launcher;
    private readonly IWorkspaceContextSeeder _seeder;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public LaunchSkillCommandHandler(ITerminalLauncher launcher, IWorkspaceContextSeeder seeder, IDbContextFactory<BishopDbContext> dbFactory)
    {
        _launcher = launcher;
        _seeder = seeder;
        _dbFactory = dbFactory;
    }

    public async Task<bool> Handle(LaunchSkillCommand request, CancellationToken cancellationToken)
    {
        await _seeder.SeedAsync(request.WorkspacePath, cancellationToken);
        var workingDirectory = await ResolveWorkingDirectoryAsync(request, cancellationToken);
        return _launcher.Launch(workingDirectory, request.RenderedCommand, request.Snap, request.ModelId);
    }

    // A batch-member card is worked in the batch's git worktree so the session lands on the batch
    // branch and merge/clean-up/review all operate on real commits. A Closed batch (worktree gone)
    // falls back to the workspace root; the first interactive launch flips Open → Working so merge's
    // Working gate is satisfied.
    private async Task<string> ResolveWorkingDirectoryAsync(LaunchSkillCommand request, CancellationToken cancellationToken)
    {
        if (request.BatchId is not { } batchId)
            return request.WorkspacePath;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null
            || batch.Status == BatchStatus.Closed
            || string.IsNullOrWhiteSpace(batch.WorktreePath)
            || !Directory.Exists(batch.WorktreePath))
            return request.WorkspacePath;

        if (batch.Status == BatchStatus.Open)
        {
            batch.TransitionToWorking();
            await db.SaveChangesAsync(cancellationToken);
        }

        return batch.WorktreePath;
    }
}
