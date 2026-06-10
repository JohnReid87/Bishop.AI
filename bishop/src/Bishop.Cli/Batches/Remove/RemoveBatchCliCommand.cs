using Bishop.App.Batches.RemoveBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Remove;

internal sealed class RemoveBatchCliCommand : Command
{
    public RemoveBatchCliCommand(ISender mediator)
        : base("remove", "Delete a closed batch record; cards stay on the board")
    {
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);

        this.SetHandler(async (string name) =>
        {
            await mediator.Send(new RemoveBatchCommand(name));
            Console.WriteLine($"Batch '{name}' removed.");
        }, nameArg);
    }
}
