using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ListCardsByWorkspace;

public sealed class ListCardsByWorkspaceQueryHandler : IRequestHandler<ListCardsByWorkspaceQuery, IReadOnlyList<Card>>
{
    private static readonly Dictionary<string, int> LanePositions = SystemLaneNames.All
        .Select((name, i) => (name, position: i + 1))
        .ToDictionary(t => t.name, t => t.position, StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public ListCardsByWorkspaceQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Card>> Handle(ListCardsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var cards = await db.Cards
            .AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId)
            .ToListAsync(cancellationToken);

        return cards
            .OrderBy(c => LanePositions.TryGetValue(c.LaneName, out var p) ? p : int.MaxValue)
            .ThenBy(c => c.Position)
            .ToList();
    }
}
