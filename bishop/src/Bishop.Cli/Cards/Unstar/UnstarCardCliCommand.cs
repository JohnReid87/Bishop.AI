using Bishop.App.Cards.UnstarCard;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Unstar;

internal sealed class UnstarCardCliCommand : Command
{
    public UnstarCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("unstar", "Remove the star from a card")
    {
        var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");

        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string prefix, string? workspace) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;
            await mediator.Send(new UnstarCardCommand(cardId));
            Console.WriteLine($"Unstarred card #{cardNumber}");
        }, cardIdArg, CommonOptions.WorkspaceOption);
    }
}
