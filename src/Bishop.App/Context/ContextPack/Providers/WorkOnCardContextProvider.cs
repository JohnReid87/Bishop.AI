using Bishop.App.Cards;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

internal sealed class WorkOnCardContextProvider : IContextProvider
{
    public string SkillName => "work-on-card";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Commit-reference convention",
        "Card model"
    };

    public async Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        if (args.Card is null)
            return new { card = (object?)null, relatedCards = Array.Empty<object>() };

        var card = await mediator.Send(new GetCardByNumberQuery(args.Card.Value, workspace.Id), cancellationToken);
        if (card is null)
            throw new InvalidOperationException($"Card #{args.Card} not found in workspace '{workspace.Name}'.");

        var relatedCards = await LoadRelatedCardsAsync(card.Description, workspace.Id, mediator, cancellationToken);

        return new
        {
            card = new
            {
                number = card.Number,
                title = card.Title,
                description = card.Description,
                laneName = card.LaneName,
                tag = card.TagName,
                isClosed = card.IsClosed
            },
            relatedCards
        };
    }

    private static async Task<object[]> LoadRelatedCardsAsync(
        string? description, Guid workspaceId, ISender mediator, CancellationToken cancellationToken)
    {
        var numbers = RelatedSectionParser.ParseCardNumbers(description);
        if (numbers.Count == 0) return [];

        var results = new List<object>(numbers.Count);
        foreach (var number in numbers)
        {
            var related = await mediator.Send(new GetCardByNumberQuery(number, workspaceId), cancellationToken);
            if (related is null) continue;
            results.Add(new { number = related.Number, title = related.Title, laneName = related.LaneName, isClosed = related.IsClosed });
        }
        return [.. results];
    }
}
