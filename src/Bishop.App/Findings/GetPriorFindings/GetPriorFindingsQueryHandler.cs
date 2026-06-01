using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Findings.GetPriorFindings;

internal sealed class GetPriorFindingsQueryHandler
    : IRequestHandler<GetPriorFindingsQuery, IReadOnlyList<PriorFindingRecord>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetPriorFindingsQueryHandler(IDbContextFactory<BishopDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<PriorFindingRecord>> Handle(
        GetPriorFindingsQuery request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Findings
            .AsNoTracking()
            .Where(f => f.Run.WorkspaceId == request.WorkspaceId
                     && f.Run.SkillName == request.SkillName)
            .OrderBy(f => f.Run.ProjectName)
            .ThenBy(f => f.Title)
            .Select(f => new PriorFindingRecord(
                f.IdentityHash,
                f.ProjectName,
                f.File,
                f.Symbol,
                f.Rule,
                f.Title,
                f.Status,
                f.RebuttalText,
                f.LinkedCardId))
            .ToListAsync(cancellationToken);
    }
}
