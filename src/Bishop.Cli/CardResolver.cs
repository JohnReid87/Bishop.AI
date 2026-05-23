using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.Core;
using MediatR;

namespace Bishop.Cli;

internal sealed class CardResolver(IMediator mediator)
{
    private readonly WorkspaceResolver _workspaceResolver = new(mediator);

    public async Task<(Guid cardId, int cardNumber, Workspace ws)?> ResolveAsync(
        string? workspaceOption, string prefix, CancellationToken cancellationToken = default)
    {
        var ws = await _workspaceResolver.ResolveAsync(workspaceOption, cancellationToken);
        var stripped = prefix.TrimStart('#');

        if (stripped.Length > 0 && stripped.All(char.IsDigit) && int.TryParse(stripped, out var number))
        {
            var card = await mediator.Send(new GetCardByNumberQuery(number, ws.Id), cancellationToken);
            if (card is null)
                throw new InvalidOperationException($"No card found matching '#{number}'.");
            return (card.Id, card.Number, ws);
        }

        var cards = await mediator.Send(new ListCardsByWorkspaceQuery(ws.Id), cancellationToken);
        var matches = cards
            .Where(c => c.Id.ToString("N").StartsWith(stripped, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0)
            throw new InvalidOperationException($"No card found matching '{stripped}'.");
        if (matches.Count > 1)
        {
            Console.Error.WriteLine($"Ambiguous prefix '{stripped}' — {matches.Count} matches:");
            foreach (var c in matches)
                Console.Error.WriteLine($"  {c.Id.ToString("N")[..8]}  {c.Title}");
            Environment.ExitCode = 1;
            return null;
        }
        return (matches[0].Id, matches[0].Number, ws);
    }
}
