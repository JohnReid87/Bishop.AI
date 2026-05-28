using Bishop.App.Cards.GetCardByNumber;
using Bishop.Core;
using MediatR;

namespace Bishop.Cli;

internal sealed class CardResolver(ISender mediator)
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

        throw new InvalidOperationException($"No card found matching '{stripped}'.");
    }
}
