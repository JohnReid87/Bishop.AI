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
        return await db.Cards
            .AsNoTracking()
            .Include(c => c.CardTags)
            .ThenInclude(ct => ct.Tag)
            .Where(c => c.Lane.WorkspaceId == request.WorkspaceId)
            .OrderBy(c => c.Lane.Position)
            .ThenBy(c => c.Position)
            .ToListAsync(cancellationToken);
    }
}
