using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.RecordSkillRun;

internal sealed class RecordSkillRunCommandHandler : IRequestHandler<RecordSkillRunCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecordSkillRunCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RecordSkillRunCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.WorkspaceSkillRuns
            .FirstOrDefaultAsync(r => r.WorkspaceId == request.WorkspaceId && r.SkillName == request.SkillName, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        if (run is null)
        {
            db.WorkspaceSkillRuns.Add(new WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                SkillName = request.SkillName,
                GitSha = request.GitSha,
                RecordedAt = now,
            });
        }
        else
        {
            run.GitSha = request.GitSha;
            run.RecordedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
