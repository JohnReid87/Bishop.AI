using Bishop.App.Batches.AddCardToBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.AddCard;

internal sealed class AddCardToBatchCliCommand : Command
{
    public AddCardToBatchCliCommand(ISender mediator, CardResolver cardResolver)
        : base("add-card", "Add a card to an Open batch")
    {
        var nameArg = new Argument<string>("name", "Batch name");
        var cardIdArg = new Argument<string>("card-id", "Card number or short ID");

        AddArgument(nameArg);
        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string prefix, string? workspace) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;

            await mediator.Send(new AddCardToBatchCommand(name, cardId));
            Console.WriteLine($"Added #{cardNumber} to batch '{name}'.");
        }, nameArg, cardIdArg, CommonOptions.WorkspaceOption);
    }
}
