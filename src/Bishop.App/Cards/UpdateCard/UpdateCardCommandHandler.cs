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
        if (request.Title is null && request.Description is null && !request.UpdateTag)
            throw new InvalidOperationException("At least one field (--title, --description, or --tag) must be supplied.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var card = await db.Cards
            .Include(c => c.Lane)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        if (request.Title is not null)
            card.Title = request.Title;

        if (request.Description is not null)
            card.Description = request.Description;

        if (request.UpdateTag)
        {
            if (string.IsNullOrEmpty(request.TagName))
            {
                card.TagId = null;
            }
            else
            {
                var tag = await db.Tags.FirstOrDefaultAsync(
                    t => t.WorkspaceId == card.Lane.WorkspaceId && t.Name == request.TagName,
                    cancellationToken);
                if (tag is null)
                {
                    tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = card.Lane.WorkspaceId, Name = request.TagName };
                    db.Tags.Add(tag);
                }
                card.TagId = tag.Id;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return card;
    }
}
