using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.GetCard;

public sealed class GetCardQueryHandler : IRequestHandler<GetCardQuery, Card?>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetCardQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Card?> Handle(GetCardQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Cards
            .AsNoTracking()
            .Include(c => c.Lane)
            .Include(c => c.CardTags)
            .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);
    }
}
