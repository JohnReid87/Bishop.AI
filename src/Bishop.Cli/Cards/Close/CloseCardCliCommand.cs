using Bishop.App.Cards.CloseCard;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Close;

internal sealed class CloseCardCliCommand : Command
{
    public CloseCardCliCommand(IMediator mediator, CardResolver cardResolver)
        : base("close", "Mark a card as closed")
    {
        var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");

        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string prefix, string? workspace) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;
            await mediator.Send(new CloseCardCommand(cardId));
            Console.WriteLine($"Closed card #{cardNumber}");
        }, cardIdArg, CommonOptions.WorkspaceOption);
    }
}
