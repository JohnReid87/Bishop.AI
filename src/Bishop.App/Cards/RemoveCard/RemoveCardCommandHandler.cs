using Bishop.Data;
using MediatR;

namespace Bishop.App.Cards.RemoveCard;

public sealed class RemoveCardCommandHandler : IRequestHandler<RemoveCardCommand, Unit>
{
    private readonly BishopDbContext _db;

    public RemoveCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Unit> Handle(RemoveCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards.FindAsync([request.CardId], cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
