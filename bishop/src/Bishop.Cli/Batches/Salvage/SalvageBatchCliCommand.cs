using Bishop.App.Batches.SalvageBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Salvage;

internal sealed class SalvageBatchCliCommand : Command
{
    public SalvageBatchCliCommand(ISender mediator)
        : base("salvage", "Deliver a partially-succeeded batch: merge the finished cards, eject the rest to To Do, and clean up")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Batch name");
        var yesOpt = new Option<bool>("--yes", "Skip the confirmation prompt");

        AddArgument(nameArg);
        AddOption(yesOpt);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, bool yes, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var result = await mediator.Send(new SalvageBatchCommand(name, ws.Path, Confirm: yes));

            if (result.Outcome == SalvageBatchOutcome.NeedsConfirmation)
            {
                PrintPlan(name, result);
                Console.Write("Proceed with salvage? [y/N] ");
                var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (answer != "y" && answer != "yes")
                {
                    Console.WriteLine("Cancelled.");
                    return;
                }

                result = await mediator.Send(new SalvageBatchCommand(name, ws.Path, Confirm: true));
            }

            switch (result.Outcome)
            {
                case SalvageBatchOutcome.NothingSucceeded:
                    Console.Error.WriteLine(
                        $"Batch '{name}' has no finished cards to deliver — nothing to salvage. Use 'bishop batch abandon {name}' to discard it.");
                    Environment.ExitCode = 1;
                    return;

                case SalvageBatchOutcome.LockAlive:
                    Console.Error.WriteLine(
                        $"Batch '{name}' is still running (lock held by live process {result.LockOwnerPid}); refusing to salvage. Stop that run first.");
                    Environment.ExitCode = 1;
                    return;

                case SalvageBatchOutcome.MergeConflict:
                    if (result.ConflictFiles is { Count: > 0 })
                    {
                        Console.Error.WriteLine($"Merge conflict in batch '{name}'. Salvage aborted; no changes made. Conflicting files:");
                        foreach (var file in result.ConflictFiles)
                            Console.Error.WriteLine($"  {file}");
                    }
                    else
                    {
                        Console.Error.WriteLine(
                            $"Cannot salvage batch '{name}': {result.ErrorMessage ?? "merge failed"}. No changes made.");
                    }
                    Environment.ExitCode = 1;
                    return;

                case SalvageBatchOutcome.Salvaged:
                    ReportSalvaged(name, result);
                    return;
            }
        }, nameArg, yesOpt, CommonOptions.WorkspaceOption);
    }

    private static void PrintPlan(string name, SalvageBatchResult result)
    {
        var merged = result.MergedCardNumbers ?? [];
        var ejected = result.EjectedCardNumbers ?? [];
        Console.WriteLine($"Salvage '{name}':");
        Console.WriteLine($"  Merge {merged.Count} finished card(s): {FormatCards(merged)}");
        Console.WriteLine($"  Eject {ejected.Count} unfinished card(s) to To Do: {FormatCards(ejected)}");
    }

    private static void ReportSalvaged(string name, SalvageBatchResult result)
    {
        var merged = result.MergedCardNumbers ?? [];
        var ejected = result.EjectedCardNumbers ?? [];
        Console.WriteLine(
            $"Salvaged '{name}': merged {merged.Count} finished card(s) ({FormatCards(merged)}); "
            + $"ejected {ejected.Count} card(s) to To Do ({FormatCards(ejected)}). Batch closed.");
    }

    private static string FormatCards(IReadOnlyList<int> numbers) =>
        numbers.Count == 0 ? "none" : "#" + string.Join(", #", numbers);
}
