using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.AddCard;

public sealed class AddCardCommandHandler : IRequestHandler<AddCardCommand, Card>
{
    private readonly BishopDbContext _db;

    public AddCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Card> Handle(AddCardCommand request, CancellationToken cancellationToken)
    {
        var position = await _db.Cards.CountAsync(c => c.LaneId == request.LaneId, cancellationToken) + 1;
        var card = new Card
        {
            Id = Guid.NewGuid(),
            LaneId = request.LaneId,
            Title = request.Title,
            Description = request.Description,
            Position = position,
        };
        _db.Cards.Add(card);
        await _db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
