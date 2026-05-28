using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.PurgeWorkspace;
using Bishop.Core;
using MediatR;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Bishop.Cli.Workspaces.Purge;

internal sealed class PurgeWorkspaceCliCommand : Command
{
    public PurgeWorkspaceCliCommand(ISender mediator)
        : base("purge", "Hard-delete an archived workspace and all its cards")
    {
        var pathOpt = new Option<string?>("--path", "Path of the archived workspace to purge");
        var nameOpt = new Option<string?>("--name", "Name of the archived workspace to purge");
        var yesOpt = new Option<bool>("--yes", "Skip confirmation prompt");
        var dryRunOpt = new Option<bool>("--dry-run", "Preview what would be deleted without modifying anything");

        AddOption(pathOpt);
        AddOption(nameOpt);
        AddOption(yesOpt);
        AddOption(dryRunOpt);

        this.SetHandler(async (InvocationContext ctx) =>
        {
            var path = ctx.ParseResult.GetValueForOption(pathOpt);
            var name = ctx.ParseResult.GetValueForOption(nameOpt);
            var yes = ctx.ParseResult.GetValueForOption(yesOpt);
            var dryRun = ctx.ParseResult.GetValueForOption(dryRunOpt);

            if (path is null && name is null)
            {
                Console.Error.WriteLine("error: specify --path or --name");
                ctx.ExitCode = 1;
                return;
            }

            var removed = (await mediator.Send(new ListWorkspacesQuery(IncludeRemoved: true)))
                .Where(w => w.IsRemoved)
                .ToList();

            Workspace ws;

            if (path is not null)
            {
                var normalizedPath = Path.GetFullPath(path);
                ws = removed.FirstOrDefault(w =>
                    !string.IsNullOrEmpty(w.Path) &&
                    string.Equals(Path.GetFullPath(w.Path), normalizedPath, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"No archived workspace found at path '{path}'.");
            }
            else
            {
                var matches = removed
                    .Where(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                    throw new InvalidOperationException($"No archived workspace found with name '{name}'.");

                if (matches.Count > 1)
                {
                    Console.Error.WriteLine($"error: multiple archived workspaces match '{name}' — re-run with --path:");
                    foreach (var m in matches)
                        Console.Error.WriteLine($"  {m.Path}");
                    ctx.ExitCode = 1;
                    return;
                }

                ws = matches[0];
            }

            var bishopDir = Path.Combine(ws.Path, ".bishop");
            var bishopDirExists = Directory.Exists(bishopDir);

            Console.WriteLine($"Workspace:  {ws.Name}");
            Console.WriteLine($"Path:       {ws.Path}");
            Console.WriteLine("Actions:");
            Console.WriteLine("  - Hard-delete workspace and all its cards from the database");
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

            await mediator.Send(new PurgeWorkspaceCommand(ws.Id));

            if (Directory.Exists(bishopDir))
                Directory.Delete(bishopDir, recursive: true);

            Console.WriteLine($"Workspace '{ws.Name}' purged.");
        });
    }
}
