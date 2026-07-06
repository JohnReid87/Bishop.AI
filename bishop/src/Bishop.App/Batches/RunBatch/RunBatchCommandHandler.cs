using Bishop.App.Cards.RecordCardFailure;
using Bishop.App.Cards.RecordCardSuccess;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Context.ContextPack;
using Bishop.App.Git;
using Bishop.App.Services.Claude;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.App.Batches.RunBatch;

internal sealed class RunBatchCommandHandler : IRequestHandler<RunBatchCommand, RunBatchResult>
{
    private readonly IGitCli _git;
    private readonly IClaudeCliRunner _claude;
    private readonly ISender _sender;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ILogger<RunBatchCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RunBatchCommandHandler(
        IGitCli git,
        IClaudeCliRunner claude,
        ISender sender,
        IDbContextFactory<BishopDbContext> dbFactory,
        ILogger<RunBatchCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _git = git;
        _claude = claude;
        _sender = sender;
        _dbFactory = dbFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<RunBatchResult> Handle(RunBatchCommand request, CancellationToken cancellationToken)
    {
        Batch batch;
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var matches = await db.Batches.ByName(request.Name).ToListAsync(cancellationToken);

            if (matches.Count == 0)
                throw new InvalidOperationException($"No batch named '{request.Name}' found.");
            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                    + string.Join(", ", matches.Select(b => b.BranchName)));

            batch = matches[0];

            if (!request.Resume)
            {
                if (batch.Status == BatchStatus.Working)
                    throw new InvalidOperationException(
                        $"Batch '{request.Name}' is already running (Working); use --resume to continue or abandon it first.");
                if (batch.Status == BatchStatus.Closed)
                    throw new InvalidOperationException($"Batch '{request.Name}' is closed.");
                batch.TransitionToWorking();
            }
            else
            {
                if (batch.Status != BatchStatus.Working)
                    throw new InvalidOperationException(
                        $"Batch '{request.Name}' is not in Working state (current: {batch.Status}); remove --resume to start a fresh run.");
            }

            batch.StoppedAt = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        await using var cardsDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var allCards = await cardsDb.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var pendingCards = request.Resume
            ? allCards.Where(c => c.LaneName != SystemLaneNames.Done).ToList()
            : allCards;

        var model = request.Model ?? batch.Model;

        var succeeded = 0;
        var failedNumbers = new List<int>();
        var batchCostUsd = 0m;

        string? mainWorkspacePath = null;
        Workspace? worktreeWorkspace = null;
        if (pendingCards.Count > 0)
        {
            var ws = await _sender.Send(new GetWorkspaceQuery(allCards[0].WorkspaceId), cancellationToken)
                ?? throw new InvalidOperationException($"Workspace not found for batch '{batch.Name}'.");
            mainWorkspacePath = ws.Path;
            worktreeWorkspace = ws.With(path: batch.WorktreePath);
        }

        var lockPath = BatchLock.LockFilePath(batch.WorktreePath, batch.Id);
        try
        {
            WriteLockFile(lockPath);
            foreach (var card in pendingCards)
            {
                RefreshLockFile(lockPath);

                await using var stopCheckDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
                var current = await stopCheckDb.Batches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == batch.Id, cancellationToken);
                if (current?.StoppedAt is not null)
                    return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.StopRequested);

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

                await _sender.Send(
                    new UpdateCardCommand(card.Id, null, null, false, null, ToLaneName: SystemLaneNames.Doing),
                    cancellationToken);

                var stamp = _timeProvider.GetLocalNow().ToString("HH:mm:ss");
                Console.Out.WriteLine($"== [{stamp}] Card #{card.Number}: {card.Title}  [{model}] ==");

                var contextPack = await _sender.Send(
                    new BuildContextPackQuery("auto-card", worktreeWorkspace!, new ContextPackArgs(card.Number)),
                    cancellationToken);
                var contextJson = JsonSerializer.Serialize(contextPack, s_contextPackOpts);
                var prompt = $"<bishop-context>\n{contextJson}\n</bishop-context>\n\n/bish-auto-card #{card.Number}";
                var runResult = await _claude.RunPromptAsync(
                    batch.WorktreePath, prompt, model, card.Number,
                    transcriptBasePath: mainWorkspacePath,
                    cancellationToken: cancellationToken);

                var runCostUsd = runResult.Totals?.CostUsd ?? 0m;
                batchCostUsd += runCostUsd;
                var costEcho = runResult.Totals is null
                    ? string.Empty
                    : $"  ·  est. {ClaudeCostFormatter.FormatUsd(runCostUsd)}  ·  batch total {ClaudeCostFormatter.FormatUsd(batchCostUsd)}";
                Console.Out.WriteLine($"exit {runResult.ExitCode}{costEcho}");

                if (runResult.ExitCode == 0)
                {
                    var handoff = await ReadHandoffAsync(batch.WorktreePath, cancellationToken);

                    if (handoff.Outcome == HandoffOutcome.Missing)
                        return await HandleCardFailureAsync(card, batch, succeeded, failedNumbers, RunBatchStopReason.HandoffMissing, runCostUsd, cancellationToken);
                    if (handoff.Outcome == HandoffOutcome.Malformed)
                        return await HandleCardFailureAsync(card, batch, succeeded, failedNumbers, RunBatchStopReason.HandoffMalformed, runCostUsd, cancellationToken);

                    var payload = handoff.Payload!;
                    var prefix = TagToPrefix(card.TagName);
                    var message = ComposeCommitMessage(prefix, card.Title, card.Number, payload.CommitBodyBullets);

                    string commitHash;
                    try
                    {
                        await _git.StageAllAsync(batch.WorktreePath, cancellationToken);
                        commitHash = await _git.CommitAsync(batch.WorktreePath, message, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Git stage/commit failed for card #{CardNumber} in batch {BatchName}", card.Number, batch.Name);
                        return await HandleCardFailureAsync(card, batch, succeeded, failedNumbers, RunBatchStopReason.CardFailure, runCostUsd, cancellationToken);
                    }

                    var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
                    var costFinding = ClaudeCostFormatter.FormatCardFinding(model, runResult.Totals);
                    await _sender.Send(
                        new RecordCardSuccessCommand(
                            card.Id,
                            commitHash,
                            batch.BranchName,
                            totals.InputTokens,
                            totals.OutputTokens,
                            totals.CacheCreationTokens,
                            totals.CacheReadTokens,
                            totals.CostUsd,
                            CombineNotes(payload.Notes, costFinding)),
                        cancellationToken);

                    succeeded++;
                }
                else
                {
                    return await HandleCardFailureAsync(card, batch, succeeded, failedNumbers, RunBatchStopReason.CardFailure, runCostUsd, cancellationToken);
                }
            }

            await SetBatchFinishedAtNowAsync(batch.Id, cancellationToken);
            return new RunBatchResult(succeeded, null, RunBatchStopReason.Finished);
        }
        finally
        {
            BatchLock.DeleteLockFile(lockPath);
        }
    }

    private async Task<RunBatchResult> HandleCardFailureAsync(
        Card card,
        Batch batch,
        int succeeded,
        List<int> failedNumbers,
        RunBatchStopReason stopReason,
        decimal runCostUsd,
        CancellationToken cancellationToken)
    {
        await _git.ResetHardAsync(batch.WorktreePath, cancellationToken);
        await _git.CleanWorkingTreeAsync(batch.WorktreePath, cancellationToken);
        await _sender.Send(new RecordCardFailureCommand(card.Id, batch.Id, runCostUsd), cancellationToken);
        failedNumbers.Add(card.Number);
        return new RunBatchResult(succeeded, ToNullableList(failedNumbers), stopReason);
    }

    private async Task SetBatchFinishedAtNowAsync(Guid batchId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var b = await db.Batches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");
        b.FinishedAt = _timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    private void WriteLockFile(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.WriteAllText(lockPath, $"{Environment.ProcessId}\t{_timeProvider.GetUtcNow():O}");
    }

    private void RefreshLockFile(string lockPath)
    {
        // intentional: best-effort lock-file refresh; failure does not interrupt batch execution
        try { File.WriteAllText(lockPath, $"{Environment.ProcessId}\t{_timeProvider.GetUtcNow():O}"); } catch { }
    }

    private async Task<HandoffReadResult> ReadHandoffAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(worktreePath, ".bishop", "handoff.json");
        if (!File.Exists(path))
            return HandoffReadResult.Missing;

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var payload = JsonSerializer.Deserialize<HandoffPayload>(json);
            if (payload is null)
            {
                PreserveMalformedHandoff(path, worktreePath);
                return HandoffReadResult.Malformed;
            }

            // intentional: best-effort handoff-file cleanup after a clean read
            try { File.Delete(path); } catch { }
            return HandoffReadResult.Valid(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialise handoff.json in {WorktreePath}", worktreePath);
            PreserveMalformedHandoff(path, worktreePath);
            return HandoffReadResult.Malformed;
        }
    }

    private void PreserveMalformedHandoff(string handoffPath, string worktreePath)
    {
        var invalidPath = Path.Combine(worktreePath, ".bishop", "handoff.invalid.json");
        // intentional: best-effort rename so a broken handoff survives for diagnosis instead of being lost
        try { File.Move(handoffPath, invalidPath, overwrite: true); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preserve malformed handoff.json as handoff.invalid.json in {WorktreePath}", worktreePath);
        }
    }

    private enum HandoffOutcome { Missing, Malformed, Valid }

    private sealed record HandoffReadResult(HandoffOutcome Outcome, HandoffPayload? Payload)
    {
        public static readonly HandoffReadResult Missing = new(HandoffOutcome.Missing, null);
        public static readonly HandoffReadResult Malformed = new(HandoffOutcome.Malformed, null);
        public static HandoffReadResult Valid(HandoffPayload payload) => new(HandoffOutcome.Valid, payload);
    }

    private static string? CombineNotes(string? notes, string? costFinding)
    {
        if (string.IsNullOrWhiteSpace(costFinding))
            return notes;
        if (string.IsNullOrWhiteSpace(notes))
            return costFinding;
        return $"{notes}\n\n{costFinding}";
    }

    private static string TagToPrefix(string? tag) => tag switch
    {
        "feature" => "feat",
        "bug" => "fix",
        "chore" => "chore",
        "docs" => "docs",
        "refactor" => "refactor",
        "test" => "test",
        _ => "chore"
    };

    private static string ComposeCommitMessage(string prefix, string title, int number, IReadOnlyList<string> bullets)
    {
        var subject = $"{prefix}: {title} (card #{number})";
        if (bullets.Count == 0)
            return subject;
        var body = string.Join("\n", bullets.Select(b => $"- {b}"));
        return $"{subject}\n\n{body}";
    }

    private static IReadOnlyList<int>? ToNullableList(List<int> list) => list.Count > 0 ? list : null;

    private static readonly JsonSerializerOptions s_contextPackOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };
}
