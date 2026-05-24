using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ListCardsByWorkspace;

public sealed class ListCardsByWorkspaceQueryHandler : IRequestHandler<ListCardsByWorkspaceQuery, IReadOnlyList<Card>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListCardsByWorkspaceQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Card>> Handle(ListCardsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Card> query = db.Cards
            .AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId);

        if (request.TagName is not null)
            query = query.Where(c => c.TagName == request.TagName);

        if (request.LaneName is not null)
            query = query.Where(c => c.LaneName == request.LaneName);

        // Push sort into SQL. Single-lane queries sort by position alone; multi-lane queries
        // use a CASE WHEN chain that matches the fixed system lane order.
        IQueryable<Card> sorted = request.LaneName is not null
            ? query.OrderBy(c => c.Position)
            : query
                .OrderBy(c =>
                    c.LaneName == SystemLaneNames.Backlog ? 0 :
                    c.LaneName == SystemLaneNames.ToDo ? 1 :
                    c.LaneName == SystemLaneNames.Doing ? 2 :
                    c.LaneName == SystemLaneNames.Done ? 3 : int.MaxValue)
                .ThenBy(c => c.Position);

        if (request.Skip > 0)
            sorted = sorted.Skip(request.Skip);
        if (request.Take < int.MaxValue)
            sorted = sorted.Take(request.Take);

        return await sorted.ToListAsync(cancellationToken);
    }
}
