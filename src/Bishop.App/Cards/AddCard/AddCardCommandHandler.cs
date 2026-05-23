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
        if (!SystemLaneNames.All.Contains(request.LaneName))
            throw new InvalidOperationException($"Lane '{request.LaneName}' is not a system lane.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var workspace = await db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        int newPosition;
        if (request.Position == CardInsertPosition.Bottom)
        {
            var maxPosition = await db.Cards
                .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == request.LaneName)
                .MaxAsync(c => (int?)c.Position, cancellationToken);
            newPosition = (maxPosition ?? 0) + 1;
        }
        else
        {
            var existing = await db.Cards
                .Where(c => c.WorkspaceId == workspace.Id && c.LaneName == request.LaneName)
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
            LaneName = request.LaneName,
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
