using System.Diagnostics;
using Bishop.App.Cards.ClaimCard;
using Bishop.Core;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Claude;
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
        var processed = 0;

        while (true)
        {
            var status = await _git.GetWorkingTreeStatusAsync(request.WorkspacePath, cancellationToken);
            switch (status)
            {
                case GetWorkingTreeStatusResult.Dirty dirty:
                    return new WorkNextResult(processed, WorkNextStopReason.DirtyWorkingTree, DirtyPaths: dirty.Paths);
                case GetWorkingTreeStatusResult.NotAGitRepo:
                    return new WorkNextResult(processed, WorkNextStopReason.NotAGitRepo);
                case GetWorkingTreeStatusResult.GitNotFound:
                    return new WorkNextResult(processed, WorkNextStopReason.GitNotFound);
            }

            var card = await _sender.Send(
                new ClaimCardCommand(request.WorkspaceId, SystemLaneNames.ToDo, request.Tag),
                cancellationToken);

            if (card is null)
                return new WorkNextResult(processed, WorkNextStopReason.EmptyLane);

            var startStamp = DateTimeOffset.Now.ToString("HH:mm:ss");
            var startLine = request.Model is not null
                ? $"== [{startStamp}] Card #{card.Number}: {card.Title}  [{request.Model}] =="
                : $"== [{startStamp}] Card #{card.Number}: {card.Title} ==";
            Console.Out.WriteLine(startLine);

            var prompt = $"/bish-auto-card #{card.Number}";
            var stopwatch = Stopwatch.StartNew();
            var runResult = await _claude.RunPromptAsync(request.WorkspacePath, prompt, request.Model, cancellationToken);
            stopwatch.Stop();

            Console.Out.WriteLine($"exit {runResult.ExitCode}");
            Console.Out.WriteLine(FormatCardSummary(card.Number, runResult, stopwatch.Elapsed));

            if (runResult.ExitCode != 0)
                return new WorkNextResult(processed, WorkNextStopReason.ClaudeFailed, FailedCardNumber: card.Number);

            var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
            await _sender.Send(
                new RecordClaudeRunCommand(card.Id, totals.InputTokens, totals.OutputTokens),
                cancellationToken);

            processed++;

            if (request.MaxIterations > 0 && processed >= request.MaxIterations)
                return new WorkNextResult(processed, WorkNextStopReason.CapReached);
        }
    }

    private static string FormatCardSummary(int cardNumber, ClaudeRunResult runResult, TimeSpan elapsed)
    {
        var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
        var toolUses = runResult.ToolUseCount == 1 ? "1 tool use" : $"{runResult.ToolUseCount} tool uses";
        var inTokens = RunFormatting.FormatTokens(totals.InputTokens);
        var outTokens = RunFormatting.FormatTokens(totals.OutputTokens);
        var duration = RunFormatting.FormatDuration(elapsed);
        return $"card #{cardNumber}: {toolUses}, {inTokens}↑ {outTokens}↓ in {duration}";
    }
}
