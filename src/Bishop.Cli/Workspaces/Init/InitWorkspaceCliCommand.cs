using Bishop.App.Workspaces.InitWorkspace;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Workspaces.Init;

internal sealed class InitWorkspaceCliCommand : Command
{
    public InitWorkspaceCliCommand(ISender mediator) : base("init", "Register a directory as a workspace")
    {
        var pathOpt = new Option<string?>("--path", "Directory to initialise (defaults to cwd)");
        var nameOpt = new Option<string?>("--name", "Workspace name (defaults to directory name)");

        AddOption(pathOpt);
        AddOption(nameOpt);

        this.SetHandler(async (string? path, string? name) =>
        {
            var dir = path ?? Directory.GetCurrentDirectory();
            var result = await mediator.Send(new InitWorkspaceCommand(dir, name));

            if (result.NeedsArchivedAction)
            {
                var archived = result.Workspace;
                Console.WriteLine($"A removed workspace '{archived.Name}' exists at this path.");
                Console.Write("Restore it or start fresh? (restore/fresh) ");
                var choice = Console.ReadLine()?.Trim().ToLowerInvariant();

                InitWorkspaceArchivedAction action;
                if (choice == "restore")
                    action = InitWorkspaceArchivedAction.Restore;
                else if (choice == "fresh")
                    action = InitWorkspaceArchivedAction.Fresh;
                else
                {
                    Console.WriteLine("Cancelled.");
                    return;
                }

                result = await mediator.Send(new InitWorkspaceCommand(dir, name, ArchivedAction: action));
            }

            var ws = result.Workspace;
            if (result.Restored)
                Console.WriteLine($"Workspace '{ws.Name}' restored at {ws.Path}");
            else if (result.Created)
                Console.WriteLine($"Initialized workspace '{ws.Name}' at {ws.Path}");
            else
                Console.WriteLine($"Workspace '{ws.Name}' is already initialized");
        }, pathOpt, nameOpt);
    }
}
