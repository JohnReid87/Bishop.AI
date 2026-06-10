using System.Text.RegularExpressions;
using Bishop.App.Cards.ListCardsByWorkspace;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bishop.ViewModels.Cards;

internal sealed class CardLinkRenderer
{
    private static readonly Regex CardRefRegex = new(
        @"(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`]*`)|(\\#\d+)|((?<!\w)#(\d+)\b)",
        RegexOptions.Compiled);

    private HashSet<int>? _validCardNumbers;

    internal async Task LoadAsync(ISender mediator, Guid workspaceId, ILogger logger, Guid cardId)
    {
        try
        {
            var cards = await mediator.Send(new ListCardsByWorkspaceQuery(workspaceId));
            _validCardNumbers = cards.Select(c => c.Number).ToHashSet();
        }
        catch (Exception ex)
        {
            // intentional: description stays unlinked when card-number list unavailable
            logger.LogDebug(ex, "Card numbers unavailable; description links disabled for card {CardId}", cardId);
        }
    }

    internal string Render(string description)
    {
        if (_validCardNumbers is null) return description;
        return CardRefRegex.Replace(description, ReplaceRef);
    }

    private string ReplaceRef(Match match)
    {
        if (match.Groups[1].Success || match.Groups[2].Success)
            return match.Value;
        var number = int.Parse(match.Groups[4].Value);
        return _validCardNumbers!.Contains(number)
            ? $"[#{number}](bishop://card/{number})"
            : $"~~#{number}~~";
    }
}
