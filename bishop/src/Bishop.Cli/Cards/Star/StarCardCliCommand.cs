using Bishop.App.Cards.StarCard;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Star;

internal sealed class StarCardCliCommand : Command
{
    public StarCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("star", "Flag a card as starred (important)")
    {
        var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");

        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string prefix, string? workspace) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;
            await mediator.Send(new StarCardCommand(cardId));
            Console.WriteLine($"Starred card #{cardNumber}");
        }, cardIdArg, CommonOptions.WorkspaceOption);
    }
}
