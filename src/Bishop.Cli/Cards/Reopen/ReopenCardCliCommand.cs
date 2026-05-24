using Bishop.App.Cards.ReopenCard;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Reopen;

internal sealed class ReopenCardCliCommand : Command
{
    public ReopenCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("reopen", "Reopen a closed card")
    {
        var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");

        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string prefix, string? workspace) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;
            await mediator.Send(new ReopenCardCommand(cardId));
            Console.WriteLine($"Reopened card #{cardNumber}");
        }, cardIdArg, CommonOptions.WorkspaceOption);
    }
}
