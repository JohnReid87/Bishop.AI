using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.GetCard;

public sealed class GetCardQueryHandler : IRequestHandler<GetCardQuery, Card?>
{
    private readonly BishopDbContext _db;

    public GetCardQueryHandler(BishopDbContext db) => _db = db;

    public async Task<Card?> Handle(GetCardQuery request, CancellationToken cancellationToken)
    {
        return await _db.Cards
            .AsNoTracking()
            .Include(c => c.Lane)
            .Include(c => c.CardTags)
            .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);
    }
}
