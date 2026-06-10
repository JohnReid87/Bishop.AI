using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Workspaces.GetWorkspaceSkillRuns;

internal sealed class GetWorkspaceSkillRunsQueryHandler : IRequestHandler<GetWorkspaceSkillRunsQuery, IReadOnlyList<WorkspaceSkillRun>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetWorkspaceSkillRunsQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<WorkspaceSkillRun>> Handle(GetWorkspaceSkillRunsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.WorkspaceSkillRuns
            .AsNoTracking()
            .Where(r => r.WorkspaceId == request.WorkspaceId)
            .OrderBy(r => r.SkillName)
            .ToListAsync(cancellationToken);
    }
}
