using Bishop.App.Cards.RemoveCard;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Remove;

internal sealed class RemoveCardCliCommand : Command
{
    public RemoveCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("remove", "Remove a card")
    {
        var cardRemoveIdArg = new Argument<string>("card-id", "Card short ID or prefix");

        AddArgument(cardRemoveIdArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string prefix, string? workspace) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;
            await mediator.Send(new RemoveCardCommand(cardId));
            Console.WriteLine($"Removed card #{cardNumber}");
        }, cardRemoveIdArg, CommonOptions.WorkspaceOption);
    }
}
