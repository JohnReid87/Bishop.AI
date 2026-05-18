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

        if (request.TagNames is { Count: > 0 })
        {
            var lane = await _db.Lanes.FindAsync([request.LaneId], cancellationToken);
            var workspaceId = lane!.WorkspaceId;

            foreach (var tagName in request.TagNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _db.Tags.FirstOrDefaultAsync(
                    t => t.WorkspaceId == workspaceId && t.Name == tagName,
                    cancellationToken);
                if (tag is null)
                {
                    tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = tagName };
                    _db.Tags.Add(tag);
                }
                _db.CardTags.Add(new CardTag { CardId = card.Id, TagId = tag.Id });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
