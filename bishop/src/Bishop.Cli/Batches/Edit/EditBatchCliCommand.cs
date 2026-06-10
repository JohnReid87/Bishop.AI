using Bishop.App.Batches.RenameBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Edit;

internal sealed class EditBatchCliCommand : Command
{
    public EditBatchCliCommand(ISender mediator) : base("edit", "Rename a batch")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Current batch name");
        var newNameOpt = new Option<string>("--new-name", "New name for the batch") { IsRequired = true };

        AddArgument(nameArg);
        AddOption(newNameOpt);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string newName, string? workspace) =>
        {
            await resolver.ResolveAsync(workspace);
            var renamed = await mediator.Send(new RenameBatchCommand(name, newName));
            Console.WriteLine($"Renamed '{name}' → '{renamed.Name}'.");
        }, nameArg, newNameOpt, CommonOptions.WorkspaceOption);
    }
}
