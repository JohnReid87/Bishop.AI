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

        var lane = await db.Lanes.FindAsync([request.LaneId], cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneId} not found.");
        var workspace = await db.Workspaces.FindAsync([lane.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {lane.WorkspaceId} not found.");

        int newPosition;
        if (request.Position == CardInsertPosition.Bottom)
        {
            var maxPosition = await db.Cards
                .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == lane.Name)
                .MaxAsync(c => (int?)c.Position, cancellationToken);
            newPosition = (maxPosition ?? 0) + 1;
        }
        else
        {
            var existing = await db.Cards
                .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == lane.Name)
                .ToListAsync(cancellationToken);
            foreach (var c in existing)
                c.Position++;
            newPosition = 1;
        }

        var number = workspace.NextCardNumber++;

        string? tagName = null;
        if (!string.IsNullOrEmpty(request.TagName))
        {
            var tag = await db.Tags.FirstOrDefaultAsync(
                t => t.WorkspaceId == workspace.Id && t.Name == request.TagName,
                cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = request.TagName };
                db.Tags.Add(tag);
            }
            tagName = tag.Name;
        }

        var card = new Card
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            LaneName = lane.Name,
            TagName = tagName,
            Title = request.Title,
            Description = request.Description,
            Number = number,
            Position = newPosition,
        };
        db.Cards.Add(card);

        await db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
