using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.GetCardByNumber;

public sealed class GetCardByNumberQueryHandler : IRequestHandler<GetCardByNumberQuery, Card?>
{
    private readonly BishopDbContext _db;

    public GetCardByNumberQueryHandler(BishopDbContext db) => _db = db;

    public async Task<Card?> Handle(GetCardByNumberQuery request, CancellationToken cancellationToken)
    {
        return await _db.Cards
            .AsNoTracking()
            .Include(c => c.Lane)
            .Include(c => c.CardTags)
            .ThenInclude(ct => ct.Tag)
            .Where(c => c.Lane.WorkspaceId == request.WorkspaceId && c.Number == request.Number)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
