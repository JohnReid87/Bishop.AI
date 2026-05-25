using Bishop.App.Batches.CompleteBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Complete;

internal sealed class CompleteBatchCliCommand : Command
{
    public CompleteBatchCliCommand(ISender mediator) : base("complete", "Merge the PR, close Done cards, and complete the batch")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            await mediator.Send(new CompleteBatchCommand(name, ws.Path));
            Console.WriteLine("Batch completed.");
        }, nameArg, CommonOptions.WorkspaceOption);
    }
}
