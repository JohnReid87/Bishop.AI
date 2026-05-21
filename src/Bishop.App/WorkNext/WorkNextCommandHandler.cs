using Bishop.App.Cards.ClaimCard;
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
                new ClaimCardCommand(request.WorkspaceId, "To Do", request.Tag),
                cancellationToken);

            if (card is null)
                return new WorkNextResult(processed, WorkNextStopReason.EmptyLane);

            Console.Out.WriteLine($"== Card #{card.Number}: {card.Title} ==");

            var prompt = $"/bish-auto-card #{card.Number}";
            var runResult = await _claude.RunPromptAsync(request.WorkspacePath, prompt, cancellationToken);

            Console.Out.WriteLine($"exit {runResult.ExitCode}");

            if (runResult.ExitCode != 0)
                return new WorkNextResult(processed, WorkNextStopReason.ClaudeFailed, FailedCardNumber: card.Number);

            var totals = runResult.Totals ?? new ClaudeRunTotals(0m, 0, 0);
            await _sender.Send(
                new RecordClaudeRunCommand(card.Id, totals.CostUsd, totals.InputTokens, totals.OutputTokens),
                cancellationToken);

            processed++;

            if (request.MaxIterations > 0 && processed >= request.MaxIterations)
                return new WorkNextResult(processed, WorkNextStopReason.CapReached);
        }
    }
}
