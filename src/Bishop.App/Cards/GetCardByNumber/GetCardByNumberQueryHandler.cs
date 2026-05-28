using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.GetCardByNumber;

public sealed class GetCardByNumberQueryHandler : IRequestHandler<GetCardByNumberQuery, Card?>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public GetCardByNumberQueryHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Card?> Handle(GetCardByNumberQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Cards
            .AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId && c.Number == request.Number)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
