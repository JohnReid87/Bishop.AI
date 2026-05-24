using Bishop.App.Workspaces.RemoveWorkspace;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Workspaces.Remove;

internal sealed class RemoveWorkspaceCliCommand : Command
{
    public RemoveWorkspaceCliCommand(ISender mediator)
        : base("remove", "Archive a workspace (soft-delete); card data is preserved")
    {
        var resolver = new WorkspaceResolver(mediator);
        var yesOpt = new Option<bool>("--yes", "Skip confirmation prompt");
        var dryRunOpt = new Option<bool>("--dry-run", "Preview what would happen without modifying anything");

        AddOption(yesOpt);
        AddOption(dryRunOpt);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (bool yes, bool dryRun, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var bishopDir = Path.Combine(ws.Path, ".bishop");
            var bishopDirExists = Directory.Exists(bishopDir);

            Console.WriteLine($"Workspace:  {ws.Name}");
            Console.WriteLine($"Path:       {ws.Path}");
            Console.WriteLine("Actions:");
            Console.WriteLine("  - Mark workspace as removed (card data preserved)");
            if (bishopDirExists)
                Console.WriteLine($"  - Delete {bishopDir}");

            if (dryRun)
            {
                Console.WriteLine("[dry-run] No changes made.");
                return;
            }

            if (!yes)
            {
                Console.Write("Proceed? [y/N] ");
                var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (answer != "y" && answer != "yes")
                {
                    Console.WriteLine("Cancelled.");
                    return;
                }
            }

            await mediator.Send(new RemoveWorkspaceCommand(ws.Id));

            if (Directory.Exists(bishopDir))
                Directory.Delete(bishopDir, recursive: true);

            Console.WriteLine($"Workspace '{ws.Name}' removed.");
        }, yesOpt, dryRunOpt, CommonOptions.WorkspaceOption);
    }
}
