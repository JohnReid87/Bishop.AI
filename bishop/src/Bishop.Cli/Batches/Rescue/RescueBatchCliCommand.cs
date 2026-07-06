using Bishop.App.Batches.RescueBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Rescue;

internal sealed class RescueBatchCliCommand : Command
{
    public RescueBatchCliCommand(ISender mediator)
        : base("rescue", "Recover an interrupted run: clear a stale lock, reset the worktree, and re-queue the stuck card")
    {
        var nameArg = new Argument<string>("name", "Batch name");
        var yesOpt = new Option<bool>("--yes", "Skip the worktree-reset confirmation prompt");

        AddArgument(nameArg);
        AddOption(yesOpt);

        this.SetHandler(async (string name, bool yes) =>
        {
            var result = await mediator.Send(new RescueBatchCommand(name, ConfirmReset: yes));

            switch (result.Outcome)
            {
                case RescueBatchOutcome.NotRunning:
                    Console.WriteLine($"Batch '{name}' is not running; no interrupted run to rescue.");
                    return;

                case RescueBatchOutcome.LockAlive:
                    Console.Error.WriteLine(
                        $"Batch '{name}' is still running (lock held by live process {result.LockOwnerPid}); refusing to rescue. Stop that run first.");
                    Environment.ExitCode = 1;
                    return;

                case RescueBatchOutcome.NeedsConfirmation:
                    Console.WriteLine("The worktree has uncommitted changes that rescue will discard:");
                    foreach (var path in result.DirtyPaths ?? [])
                        Console.WriteLine($"  {path}");
                    Console.Write("Reset the worktree and discard these changes? [y/N] ");
                    var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (answer != "y" && answer != "yes")
                    {
                        Console.WriteLine("Cancelled.");
                        return;
                    }

                    result = await mediator.Send(new RescueBatchCommand(name, ConfirmReset: true));
                    break;
            }

            ReportRescued(name, result);
        }, nameArg, yesOpt);
    }

    private static void ReportRescued(string name, RescueBatchResult result)
    {
        var actions = new List<string>();
        if (result.LockCleared)
            actions.Add("cleared the stale lock");
        if (result.WorktreeReset)
            actions.Add($"reset the worktree ({result.DirtyPaths?.Count ?? 0} file(s))");
        var requeued = result.RequeuedCardNumbers ?? [];
        if (requeued.Count > 0)
            actions.Add($"re-queued card(s) #{string.Join(", #", requeued)} to To Do");

        if (actions.Count == 0)
        {
            Console.WriteLine($"Batch '{name}' needed no repair. Run 'batch run --resume' to continue.");
            return;
        }

        Console.WriteLine($"Rescued '{name}': {string.Join("; ", actions)}.");
        Console.WriteLine("Run 'batch run --resume' to continue.");
    }
}
