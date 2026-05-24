using System.Diagnostics;
using Bishop.App.Cards.ClaimCard;
using Bishop.Core;
using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Services.Claude;
using Bishop.App.Git;
using MediatR;

namespace Bishop.App.WorkNext;

public sealed class WorkNextCommandHandler : IRequestHandler<WorkNextCommand, WorkNextResult>
{
    private readonly IGitCli _git;
    private readonly ISender _sender;
    private readonly IClaudeCliRunner _claude;

    public WorkNextCommandHandler(IGitCli git, ISender sender, IClaudeCliRunner claude)
    {
        _git = git;
        _sender = sender;
        _claude = claude;
    }

    public async Task<WorkNextResult> Handle(WorkNextCommand request, CancellationToken cancellationToken)
    {
        var bishopDir = Path.Combine(request.WorkspacePath, ".bishop");
        var runningFile = Path.Combine(bishopDir, "worknext.running");
        var stopFile = Path.Combine(bishopDir, "worknext.stop");

        Directory.CreateDirectory(bishopDir);

        if (File.Exists(stopFile))
            File.Delete(stopFile);

        var heartbeat = $"{Environment.ProcessId}{Environment.NewLine}{DateTimeOffset.UtcNow:O}{Environment.NewLine}";
        File.WriteAllText(runningFile, heartbeat);

        try
        {
            var succeeded = 0;
            var failedCardNumbers = new List<int>();

            while (true)
            {
                if (File.Exists(stopFile))
                {
                    File.Delete(stopFile);
                    return new WorkNextResult(succeeded, WorkNextStopReason.Cancelled, ToNullableList(failedCardNumbers));
                }

                var gitSw = Stopwatch.StartNew();
                var status = await _git.GetWorkingTreeStatusAsync(request.WorkspacePath, cancellationToken);
                gitSw.Stop();

                switch (status)
                {
                    case GetWorkingTreeStatusResult.Dirty dirty:
                        return new WorkNextResult(succeeded, WorkNextStopReason.DirtyWorkingTree, ToNullableList(failedCardNumbers), DirtyPaths: dirty.Paths);
                    case GetWorkingTreeStatusResult.NotAGitRepo:
                        return new WorkNextResult(succeeded, WorkNextStopReason.NotAGitRepo, ToNullableList(failedCardNumbers));
                    case GetWorkingTreeStatusResult.GitNotFound:
                        return new WorkNextResult(succeeded, WorkNextStopReason.GitNotFound, ToNullableList(failedCardNumbers));
                }

                var claimSw = Stopwatch.StartNew();
                var card = await _sender.Send(
                    new ClaimCardCommand(request.WorkspaceId, SystemLaneNames.ToDo, request.Tag),
                    cancellationToken);
                claimSw.Stop();

                if (card is null)
                    return new WorkNextResult(succeeded, WorkNextStopReason.EmptyLane, ToNullableList(failedCardNumbers));

                var startStamp = DateTimeOffset.Now.ToString("HH:mm:ss");
                var startLine = request.Model is not null
                    ? $"== [{startStamp}] Card #{card.Number}: {card.Title}  [{request.Model}] =="
                    : $"== [{startStamp}] Card #{card.Number}: {card.Title} ==";
                Console.Out.WriteLine(startLine);

                var prompt = $"/bish-auto-card #{card.Number}";
                var claudeSw = Stopwatch.StartNew();
                var runResult = await _claude.RunPromptAsync(request.WorkspacePath, prompt, request.Model, card.Number, cancellationToken);
                claudeSw.Stop();

                var recordElapsed = TimeSpan.Zero;
                if (runResult.ExitCode == 0)
                {
                    var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
                    var recordSw = Stopwatch.StartNew();
                    await _sender.Send(
                        new RecordClaudeRunCommand(card.Id, totals.InputTokens, totals.OutputTokens, totals.CacheCreationTokens, totals.CacheReadTokens),
                        cancellationToken);
                    recordSw.Stop();
                    recordElapsed = recordSw.Elapsed;
                    succeeded++;
                }

                Console.Out.WriteLine($"exit {runResult.ExitCode}");
                Console.Out.WriteLine(FormatCardSummary(card.Number, runResult, gitSw.Elapsed, claimSw.Elapsed, claudeSw.Elapsed, recordElapsed));

                if (runResult.ExitCode != 0)
                {
                    await _git.ResetHardAsync(request.WorkspacePath, cancellationToken);
                    await _git.CleanWorkingTreeAsync(request.WorkspacePath, cancellationToken);
                    await _sender.Send(new RecordAutoRunFailureCommand(card.Id), cancellationToken);
                    failedCardNumbers.Add(card.Number);
                    continue;
                }

                if (request.MaxIterations > 0 && succeeded >= request.MaxIterations)
                    return new WorkNextResult(succeeded, WorkNextStopReason.CapReached, ToNullableList(failedCardNumbers));
            }
        }
        finally
        {
            if (File.Exists(runningFile))
                File.Delete(runningFile);
        }
    }

    private static IReadOnlyList<int>? ToNullableList(List<int> list) => list.Count > 0 ? list : null;

    private static string FormatCardSummary(
        int cardNumber,
        ClaudeRunResult runResult,
        TimeSpan gitElapsed,
        TimeSpan claimElapsed,
        TimeSpan claudeElapsed,
        TimeSpan recordElapsed)
    {
        var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
        var toolUses = runResult.ToolUseCount == 1 ? "1 tool use" : $"{runResult.ToolUseCount} tool uses";
        var inTokens = RunFormatting.FormatTokens(totals.InputTokens);
        var outTokens = RunFormatting.FormatTokens(totals.OutputTokens);
        var totalCached = totals.CacheCreationTokens + totals.CacheReadTokens;
        var cachedPart = totalCached > 0 ? $" (+{RunFormatting.FormatTokens(totalCached)} cached)" : string.Empty;
        var duration = RunFormatting.FormatDuration(claudeElapsed);
        var steps = $"(git {RunFormatting.FormatDuration(gitElapsed)} · claim {RunFormatting.FormatDuration(claimElapsed)} · claude {RunFormatting.FormatDuration(claudeElapsed)} · record {RunFormatting.FormatDuration(recordElapsed)})";
        return $"card #{cardNumber}: {toolUses}, {inTokens}↑ {outTokens}↓{cachedPart} in {duration} {steps}";
    }
}
