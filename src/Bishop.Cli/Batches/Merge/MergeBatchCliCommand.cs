using Bishop.App.Batches.MergeBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Merge;

internal sealed class MergeBatchCliCommand : Command
{
    public MergeBatchCliCommand(ISender mediator) : base("merge", "Merge the batch branch into the base branch with --no-ff")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var result = await mediator.Send(new MergeBatchCommand(name, ws.Path));
            if (!result.Success)
            {
                Console.Error.WriteLine($"Merge conflict in batch '{name}'. Conflicting files:");
                foreach (var file in result.ConflictFiles)
                    Console.Error.WriteLine($"  {file}");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine("Batch merged.");
        }, nameArg, CommonOptions.WorkspaceOption);
    }
}
