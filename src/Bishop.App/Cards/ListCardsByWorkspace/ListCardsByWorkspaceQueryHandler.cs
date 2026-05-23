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

        var lanePositions = await db.Lanes
            .AsNoTracking()
            .Where(l => l.WorkspaceId == request.WorkspaceId)
            .ToDictionaryAsync(l => l.Name, l => l.Position, cancellationToken);

        var cards = await db.Cards
            .AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId)
            .ToListAsync(cancellationToken);

        return cards
            .OrderBy(c => lanePositions.TryGetValue(c.LaneName, out var p) ? p : int.MaxValue)
            .ThenBy(c => c.Position)
            .ToList();
    }
}
