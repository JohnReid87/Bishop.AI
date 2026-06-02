using Bishop.App.Cards.MoveCard;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.UpdateCard;

internal sealed class UpdateCardCommandHandler : IRequestHandler<UpdateCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;

    public UpdateCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, ISender sender)
    {
        _dbFactory = dbFactory;
        _sender = sender;
    }

    public async Task<Card> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
    {
        if (request.Title is null && request.Description is null && request.AppendDescription is null
            && !request.UpdateTag && request.ToLaneName is null)
            throw new InvalidOperationException("At least one field (--title, --description, or --tag) must be supplied.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var card = await db.Cards.FindAsync([request.CardId], cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        if (request.Title is not null)
            card.Title = request.Title;

        if (request.Description is not null)
            card.Description = request.Description;

        if (request.AppendDescription is not null)
        {
            card.Description = string.IsNullOrEmpty(card.Description)
                ? request.AppendDescription
                : $"{card.Description}\n\n---\n\n{request.AppendDescription}";
        }

        if (request.UpdateTag)
        {
            if (string.IsNullOrEmpty(request.TagName))
            {
                card.TagName = null;
            }
            else
            {
                if (!BrandTagPalette.DefaultColours.ContainsKey(request.TagName))
                    throw new InvalidOperationException($"Tag '{request.TagName}' is not a known tag.");
                card.TagName = request.TagName;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (request.ToLaneName is not null)
            card = await _sender.Send(new MoveCardCommand(card.Id, request.ToLaneName, 1), cancellationToken);

        return card;
    }
}
