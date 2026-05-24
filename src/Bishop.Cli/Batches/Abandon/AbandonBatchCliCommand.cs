using Bishop.App.Batches.AbandonBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Abandon;

internal sealed class AbandonBatchCliCommand : Command
{
    public AbandonBatchCliCommand(ISender mediator) : base("abandon", "Revert card lanes, remove the worktree, and close the batch")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);

            var result = await mediator.Send(new AbandonBatchCommand(name, ws.Path));
            Console.WriteLine($"Abandoned '{name}': {result.CardsRestored} card(s) restored to To Do.");
        }, nameArg, CommonOptions.WorkspaceOption);
    }
}
