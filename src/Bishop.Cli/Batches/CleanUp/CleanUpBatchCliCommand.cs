using Bishop.App.Batches.CleanUpBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.CleanUp;

internal sealed class CleanUpBatchCliCommand : Command
{
    public CleanUpBatchCliCommand(ISender mediator)
        : base("clean-up", "Remove worktree, delete branch, and close the batch (requires merge first)")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var result = await mediator.Send(new CleanUpBatchCommand(name, ws.Path));
            foreach (var number in result.ClosedCardNumbers)
                Console.WriteLine($"Closed card #{number}");
            Console.WriteLine($"Batch cleaned up. ({result.ClosedCardNumbers.Count} card(s) closed)");
        }, nameArg, CommonOptions.WorkspaceOption);
    }
}
