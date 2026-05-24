using Bishop.App.Cards.GetCardByNumber;
using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

public sealed class GrillMeContextProvider : IContextProvider
{
    public string SkillName => "grill-me";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Card Granularity Rules",
        "Task List Preview Format",
        "Card Push Procedure",
        "Source Card Closing Prompt"
    };

    public async Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        if (args.Card is null) return new { card = (object?)null };

        var card = await mediator.Send(new GetCardByNumberQuery(args.Card.Value, workspace.Id), cancellationToken);
        if (card is null)
            throw new InvalidOperationException($"Card #{args.Card} not found in workspace '{workspace.Name}'.");

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
            }
        };
    }
}
