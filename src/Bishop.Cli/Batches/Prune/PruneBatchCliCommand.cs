using Bishop.App.Batches.DeleteBatchBranch;
using Bishop.App.Batches.GetBatchPruneCandidates;
using Bishop.Core;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Prune;

internal sealed class PruneBatchCliCommand : Command
{
    public PruneBatchCliCommand(ISender mediator)
        : base("prune", "Delete local branches for Closed batches")
    {
        var abandonedOnlyOpt = new Option<bool>("--abandoned-only", "Only prune branches from Abandoned batches");
        var mergedOnlyOpt = new Option<bool>("--merged-only", "Only prune branches from Finished batches");
        var olderThanOpt = new Option<string?>("--older-than", "Only prune batches closed longer ago than this duration (e.g. 7d, 24h, 30m)");
        var dryRunOpt = new Option<bool>("--dry-run", "List candidates without deleting anything");
        var yesOpt = new Option<bool>("--yes", "Delete all candidates without prompting");

        AddOption(abandonedOnlyOpt);
        AddOption(mergedOnlyOpt);
        AddOption(olderThanOpt);
        AddOption(dryRunOpt);
        AddOption(yesOpt);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(
            async (bool abandonedOnly, bool mergedOnly, string? olderThan, bool dryRun, bool yes, string? workspace) =>
            {
                var resolver = new WorkspaceResolver(mediator);
                var ws = await resolver.ResolveAsync(workspace);

                TimeSpan? olderThanSpan = null;
                if (olderThan is not null)
                {
                    olderThanSpan = ParseDuration(olderThan);
                    if (olderThanSpan is null)
                    {
                        Console.Error.WriteLine($"error: cannot parse --older-than '{olderThan}' — use e.g. 7d, 24h, 30m");
                        Environment.ExitCode = 1;
                        return;
                    }
                }

                var candidates = await mediator.Send(
                    new GetBatchPruneCandidatesQuery(ws.Path, abandonedOnly, mergedOnly, olderThanSpan));

                if (candidates.Count == 0)
                {
                    Console.WriteLine("No candidates found.");
                    return;
                }

                Console.WriteLine($"{"Branch",-42} {"Reason",-10} {"Age",-10} {"Commits",7}");
                Console.WriteLine(new string('-', 74));
                foreach (var c in candidates)
                {
                    var age = FormatAge(DateTimeOffset.UtcNow - c.ClosedAt);
                    var suffix = c.IsCheckedOut ? "  (checked out — skipped)" : string.Empty;
                    Console.WriteLine($"{c.BranchName,-42} {c.ClosedReason,-10} {age,-10} {c.CommitCount,7}{suffix}");
                }

                if (dryRun)
                {
                    Console.WriteLine("[dry-run] No changes made.");
                    return;
                }

                var prunable = candidates.Where(c => !c.IsCheckedOut).ToList();
                if (prunable.Count == 0)
                {
                    Console.WriteLine("All candidates are currently checked out — nothing to prune.");
                    return;
                }

                var pruned = 0;
                foreach (var c in prunable)
                {
                    if (!yes)
                    {
                        Console.Write($"Delete '{c.BranchName}'? [y/N] ");
                        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                        if (answer != "y" && answer != "yes")
                            continue;
                    }

                    await mediator.Send(new DeleteBatchBranchCommand(ws.Path, c.BranchName));
                    Console.WriteLine($"Deleted '{c.BranchName}'.");
                    pruned++;
                }

                Console.WriteLine($"{pruned} branch(es) deleted.");
            },
            abandonedOnlyOpt, mergedOnlyOpt, olderThanOpt, dryRunOpt, yesOpt, CommonOptions.WorkspaceOption);
    }

    private static TimeSpan? ParseDuration(string input)
    {
        var s = input.Trim();
        if (s.Length < 2) return null;
        var suffix = s[^1];
        if (!int.TryParse(s[..^1], out var value) || value < 0) return null;
        return suffix switch
        {
            'd' => TimeSpan.FromDays(value),
            'h' => TimeSpan.FromHours(value),
            'm' => TimeSpan.FromMinutes(value),
            _ => null,
        };
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1) return $"{(int)age.TotalDays}d";
        if (age.TotalHours >= 1) return $"{(int)age.TotalHours}h";
        return $"{(int)age.TotalMinutes}m";
    }
}
