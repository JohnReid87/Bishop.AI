using Bishop.App.Batches.RunBatch;
using Bishop.App.Skills;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Run;

internal sealed class RunBatchCliCommand : Command
{
    public RunBatchCliCommand(ISender mediator) : base("run", "Run a batch end-to-end in its worktree")
    {
        var nameArg = new Argument<string>("name", "Batch name");
        var resumeOpt = new Option<bool>("--resume", "Re-acquire the lock and continue from the next undone card");
        var modelOpt = new Option<string>("--model", () => SkillModelOptions.DefaultModelId, "Claude model ID to pass to claude");

        AddArgument(nameArg);
        AddOption(resumeOpt);
        AddOption(modelOpt);

        this.SetHandler(async (string name, bool resume, string model) =>
        {
            var result = await mediator.Send(new RunBatchCommand(name, resume, model));

            var failedNumbers = result.FailedCardNumbers ?? Array.Empty<int>();
            var stamp = DateTimeOffset.Now.ToString("HH:mm:ss");
            var summary = $"[{stamp}] Processed: {result.Succeeded + failedNumbers.Count} · Succeeded: {result.Succeeded} · Failed: {failedNumbers.Count}";
            if (failedNumbers.Count > 0)
                summary += $" (#{string.Join(", #", failedNumbers)})";
            Console.Out.WriteLine(summary + ".");

            switch (result.StopReason)
            {
                case RunBatchStopReason.Finished:
                    break;
                case RunBatchStopReason.CardFailure:
                    Console.Error.WriteLine("Batch stopped on card failure; resolve and --resume or abandon.");
                    Environment.ExitCode = 1;
                    break;
                case RunBatchStopReason.HandoffMissing:
                    Console.Error.WriteLine("Batch stopped: card exited 0 but wrote no valid handoff.json; resolve and --resume or abandon.");
                    Environment.ExitCode = 1;
                    break;
                case RunBatchStopReason.DirtyWorktree:
                    Console.Error.WriteLine("Worktree is dirty:");
                    foreach (var path in result.DirtyPaths ?? Array.Empty<string>())
                        Console.Error.WriteLine($"  {path}");
                    Environment.ExitCode = 1;
                    break;
                case RunBatchStopReason.NotAGitRepo:
                    Console.Error.WriteLine("Worktree is not a git repository.");
                    Environment.ExitCode = 1;
                    break;
                case RunBatchStopReason.GitNotFound:
                    Console.Error.WriteLine("'git' executable not found on PATH.");
                    Environment.ExitCode = 1;
                    break;
            }

            if (failedNumbers.Count > 0)
            {
                foreach (var n in failedNumbers)
                    Console.Error.WriteLine($"Card #{n} left in 'Doing'.");
                Environment.ExitCode = 1;
            }
        }, nameArg, resumeOpt, modelOpt);
    }
}
