using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using Bishop.App.Services.Claude;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.RunBatch;

public sealed class RunBatchCommandHandler : IRequestHandler<RunBatchCommand, RunBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IGitCli _git;
    private readonly IClaudeCliRunner _claude;
    private readonly ISender _sender;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RunBatchCommandHandler(
        IBatchRepository batches,
        IGitCli git,
        IClaudeCliRunner claude,
        ISender sender,
        IDbContextFactory<BishopDbContext> dbFactory)
    {
        _batches = batches;
        _git = git;
        _claude = claude;
        _sender = sender;
        _dbFactory = dbFactory;
    }

    public async Task<RunBatchResult> Handle(RunBatchCommand request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        if (!request.Resume)
        {
            if (batch.Status == BatchStatus.Working)
                throw new InvalidOperationException(
                    $"Batch '{request.Name}' is already running (Working); use --resume to continue or abandon it first.");
            if (batch.Status == BatchStatus.Closed)
                throw new InvalidOperationException($"Batch '{request.Name}' is closed.");
            batch = await _batches.TransitionToWorkingAsync(batch.Id, cancellationToken);
        }
        else
        {
            if (batch.Status != BatchStatus.Working)
                throw new InvalidOperationException(
                    $"Batch '{request.Name}' is not in Working state (current: {batch.Status}); remove --resume to start a fresh run.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var allCards = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var pendingCards = request.Resume
            ? allCards.Where(c => c.LaneName != SystemLaneNames.Done).ToList()
            : allCards;

        var succeeded = 0;
        var failedNumbers = new List<int>();

        foreach (var card in pendingCards)
        {
            var gitStatus = await _git.GetWorkingTreeStatusAsync(batch.WorktreePath, cancellationToken);
            switch (gitStatus)
            {
                case GetWorkingTreeStatusResult.Dirty dirty:
                    return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.DirtyWorktree, dirty.Paths);
                case GetWorkingTreeStatusResult.NotAGitRepo:
                    return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.NotAGitRepo);
                case GetWorkingTreeStatusResult.GitNotFound:
                    return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.GitNotFound);
            }

            var stamp = DateTimeOffset.Now.ToString("HH:mm:ss");
            var startLine = request.Model is not null
                ? $"== [{stamp}] Card #{card.Number}: {card.Title}  [{request.Model}] =="
                : $"== [{stamp}] Card #{card.Number}: {card.Title} ==";
            Console.Out.WriteLine(startLine);

            var prompt = $"/bish-auto-card #{card.Number}";
            var runResult = await _claude.RunPromptAsync(batch.WorktreePath, prompt, request.Model, cancellationToken);

            Console.Out.WriteLine($"exit {runResult.ExitCode}");

            if (runResult.ExitCode == 0)
            {
                var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
                await _sender.Send(
                    new RecordClaudeRunCommand(card.Id, totals.InputTokens, totals.OutputTokens, totals.CacheCreationTokens, totals.CacheReadTokens),
                    cancellationToken);

                var commitResult = await _git.GetCardCommitAsync(card.Number, batch.WorktreePath, cancellationToken);
                if (commitResult is GetCardCommitResult.Found found)
                    await _sender.Send(new SetCardCommitCommand(card.Id, found.Commit.FullHash, batch.BranchName), cancellationToken);

                succeeded++;
            }
            else
            {
                await _git.ResetHardAsync(batch.WorktreePath, cancellationToken);
                await _git.CleanWorkingTreeAsync(batch.WorktreePath, cancellationToken);
                await _sender.Send(new RecordAutoRunFailureCommand(card.Id), cancellationToken);
                failedNumbers.Add(card.Number);
                return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.CardFailure);
            }
        }

        await _batches.CloseAsync(batch.Id, BatchClosedReason.Finished, cancellationToken);
        return new RunBatchResult(succeeded, null, RunBatchStopReason.Finished);
    }

    private static IReadOnlyList<int>? ToNullableList(List<int> list) => list.Count > 0 ? list : null;
}
