using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RemoveCard;

public sealed class RemoveCardCommandHandler : IRequestHandler<RemoveCardCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RemoveCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(RemoveCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards.FindAsync([request.CardId], cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        db.Cards.Remove(card);
        await db.SaveChangesAsync(cancellationToken);
    }
}
