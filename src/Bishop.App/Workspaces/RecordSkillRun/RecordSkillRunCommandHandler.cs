using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.RecordSkillRun;

public sealed class RecordSkillRunCommandHandler : IRequestHandler<RecordSkillRunCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RecordSkillRunCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(RecordSkillRunCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.WorkspaceSkillRuns
            .FirstOrDefaultAsync(r => r.WorkspaceId == request.WorkspaceId && r.SkillName == request.SkillName, cancellationToken);

        if (run is null)
        {
            db.WorkspaceSkillRuns.Add(new WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                SkillName = request.SkillName,
                GitSha = request.GitSha,
                RecordedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            run.GitSha = request.GitSha;
            run.RecordedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
