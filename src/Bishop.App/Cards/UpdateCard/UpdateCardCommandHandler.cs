using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.UpdateCard;

public sealed class UpdateCardCommandHandler : IRequestHandler<UpdateCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public UpdateCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<Card> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
    {
        if (request.Title is null && request.Description is null && !request.UpdateTags)
            throw new InvalidOperationException("At least one field (--title, --description, --tag, or --clear-tags) must be supplied.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var card = await db.Cards
            .Include(c => c.Lane)
            .Include(c => c.CardTags)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        if (request.Title is not null)
            card.Title = request.Title;

        if (request.Description is not null)
            card.Description = request.Description;

        if (request.UpdateTags)
        {
            db.CardTags.RemoveRange(card.CardTags);

            foreach (var tagName in request.TagNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await db.Tags.FirstOrDefaultAsync(
                    t => t.WorkspaceId == card.Lane.WorkspaceId && t.Name == tagName,
                    cancellationToken);
                if (tag is null)
                {
                    tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = card.Lane.WorkspaceId, Name = tagName };
                    db.Tags.Add(tag);
                }
                db.CardTags.Add(new CardTag { CardId = card.Id, TagId = tag.Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
