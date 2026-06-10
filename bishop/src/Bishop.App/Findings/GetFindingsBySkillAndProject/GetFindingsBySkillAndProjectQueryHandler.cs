using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Findings.GetFindingsBySkillAndProject;

internal sealed class GetFindingsBySkillAndProjectQueryHandler
    : IRequestHandler<GetFindingsBySkillAndProjectQuery, IReadOnlyList<FindingRecord>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetFindingsBySkillAndProjectQueryHandler(IDbContextFactory<BishopDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<FindingRecord>> Handle(
        GetFindingsBySkillAndProjectQuery request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Findings
            .AsNoTracking()
            .Where(f => f.Run.WorkspaceId == request.WorkspaceId
                     && f.Run.SkillName == request.SkillName);

        query = request.ProjectName is null
            ? query.Where(f => f.Run.ProjectName == null)
            : query.Where(f => f.Run.ProjectName == request.ProjectName);

        var findings = await query
            .Select(f => new FindingRecord(
                f.Id,
                f.Title,
                f.Body,
                f.Severity,
                f.File,
                f.Symbol,
                f.Rule,
                f.Status,
                f.RebuttalText,
                f.LinkedCardId,
                f.LinkedCardId == null
                    ? null
                    : db.Cards
                        .Where(c => c.WorkspaceId == f.Run.WorkspaceId
                                 && c.Number == f.LinkedCardId)
                        .Select(c => (bool?)c.IsClosed)
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return findings;
    }
}
