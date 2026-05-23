using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.AddCard;

public sealed class AddCardCommandHandler : IRequestHandler<AddCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AddCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Card> Handle(AddCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        int newPosition;
        if (request.Position == CardInsertPosition.Bottom)
        {
            var maxPosition = await db.Cards
                .Where(c => c.LaneId == request.LaneId)
                .MaxAsync(c => (int?)c.Position, cancellationToken);
            newPosition = (maxPosition ?? 0) + 1;
        }
        else
        {
            var existing = await db.Cards
                .Where(c => c.LaneId == request.LaneId)
                .ToListAsync(cancellationToken);
            foreach (var c in existing)
                c.Position++;
            newPosition = 1;
        }

        var lane = await db.Lanes.FindAsync([request.LaneId], cancellationToken);
        var workspace = await db.Workspaces.FindAsync([lane!.WorkspaceId], cancellationToken);
        var number = workspace!.NextCardNumber++;

        var card = new Card
        {
            Id = Guid.NewGuid(),
            LaneId = request.LaneId,
            Title = request.Title,
            Description = request.Description,
            Number = number,
            Position = newPosition,
        };
        db.Cards.Add(card);

        if (!string.IsNullOrEmpty(request.TagName))
        {
            var workspaceId = lane.WorkspaceId;
            var tag = await db.Tags.FirstOrDefaultAsync(
                t => t.WorkspaceId == workspaceId && t.Name == request.TagName,
                cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = request.TagName };
                db.Tags.Add(tag);
            }
            card.TagId = tag.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
