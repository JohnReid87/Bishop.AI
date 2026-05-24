using Bishop.App.WorkNext;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.WorkNext;

internal sealed class WorkNextCliCommand : Command
{
    public WorkNextCliCommand(ISender mediator)
        : base("work-next", "Loop: claim a tagged card and run claude on it until exhaustion, failure, or cap")
    {
        var resolver = new WorkspaceResolver(mediator);

        var tagOpt = new Option<string?>("--tag", () => null, "Only claim cards carrying this tag (omit for any tag)");
        var maxOpt = new Option<int>("--max", () => 10, "Max cards to process; 0 means uncapped");
        var modelOpt = new Option<string?>("--model", () => "claude-sonnet-4-6", "Claude model ID to pass to claude (default: claude-sonnet-4-6)");

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(tagOpt);
        AddOption(maxOpt);
        AddOption(modelOpt);

        this.SetHandler(async (string? workspace, string? tag, int max, string? model) =>
        {
            if (max < 0)
            {
                Console.Error.WriteLine("--max must be >= 0 (0 means uncapped).");
                Environment.ExitCode = 1;
                return;
            }

            var ws = await resolver.ResolveAsync(workspace);
            var result = await mediator.Send(new WorkNextCommand(ws.Id, ws.Path, tag, max, model));

            var failedNumbers = result.FailedCardNumbers ?? Array.Empty<int>();
            var total = result.Succeeded + failedNumbers.Count;
            var stamp = DateTimeOffset.Now.ToString("HH:mm:ss");
            var summary = $"[{stamp}] Processed: {total} · Succeeded: {result.Succeeded} · Failed: {failedNumbers.Count}";
            if (failedNumbers.Count > 0)
                summary += $" (#{string.Join(", #", failedNumbers)})";
            Console.Out.WriteLine(summary + ".");

            switch (result.StopReason)
            {
                case WorkNextStopReason.EmptyLane:
                case WorkNextStopReason.CapReached:
                case WorkNextStopReason.Cancelled:
                    break;
                case WorkNextStopReason.DirtyWorkingTree:
                    Console.Error.WriteLine($"Working tree at '{ws.Path}' is dirty:");
                    foreach (var path in result.DirtyPaths ?? Array.Empty<string>())
                        Console.Error.WriteLine($"  {path}");
                    Environment.ExitCode = 1;
                    break;
                case WorkNextStopReason.NotAGitRepo:
                    Console.Error.WriteLine($"Workspace '{ws.Path}' is not a git repository.");
                    Environment.ExitCode = 1;
                    break;
                case WorkNextStopReason.GitNotFound:
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
        }, CommonOptions.WorkspaceOption, tagOpt, maxOpt, modelOpt);
    }
}
