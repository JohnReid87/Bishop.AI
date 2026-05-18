using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ListCardsByWorkspace;

public sealed class ListCardsByWorkspaceQueryHandler : IRequestHandler<ListCardsByWorkspaceQuery, IReadOnlyList<Card>>
{
    private readonly BishopDbContext _db;

    public ListCardsByWorkspaceQueryHandler(BishopDbContext db) => _db = db;

    public async Task<IReadOnlyList<Card>> Handle(ListCardsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        return await _db.Cards
            .Include(c => c.CardTags)
            .ThenInclude(ct => ct.Tag)
            .Where(c => c.Lane.WorkspaceId == request.WorkspaceId)
            .OrderBy(c => c.Lane.Position)
            .ThenBy(c => c.Position)
            .ToListAsync(cancellationToken);
    }
}
