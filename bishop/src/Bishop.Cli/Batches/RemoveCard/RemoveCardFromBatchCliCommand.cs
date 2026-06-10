using Bishop.App.Batches.RemoveCardFromBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.RemoveCard;

internal sealed class RemoveCardFromBatchCliCommand : Command
{
    public RemoveCardFromBatchCliCommand(ISender mediator, CardResolver cardResolver)
        : base("remove-card", "Remove a card from an Open batch")
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

            await mediator.Send(new RemoveCardFromBatchCommand(name, cardId));
            Console.WriteLine($"Removed #{cardNumber} from batch '{name}'.");
        }, nameArg, cardIdArg, CommonOptions.WorkspaceOption);
    }
}
