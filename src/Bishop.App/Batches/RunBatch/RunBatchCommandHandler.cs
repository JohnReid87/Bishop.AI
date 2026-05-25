using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Context.ContextPack;
using Bishop.App.Git;
using Bishop.App.Services.Claude;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        Workspace? worktreeWorkspace = null;
        if (pendingCards.Count > 0)
        {
            var ws = await _sender.Send(new GetWorkspaceQuery(allCards[0].WorkspaceId), cancellationToken)
                ?? throw new InvalidOperationException($"Workspace not found for batch '{batch.Name}'.");
            worktreeWorkspace = CreateBatchWorkspace(ws, batch.WorktreePath);
        }

        var lockPath = LockFilePath(batch.WorktreePath, batch.Id);
        try
        {
            WriteLockFile(lockPath);
            foreach (var card in pendingCards)
            {
                RefreshLockFile(lockPath);

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

                var stamp = DateTimeOffset.Now.ToString("HH:mm:ss");
                var startLine = request.Model is not null
                    ? $"== [{stamp}] Card #{card.Number}: {card.Title}  [{request.Model}] =="
                    : $"== [{stamp}] Card #{card.Number}: {card.Title} ==";
                Console.Out.WriteLine(startLine);

                var contextPack = await _sender.Send(
                    new BuildContextPackQuery("auto-card", worktreeWorkspace!, new ContextPackArgs(card.Number)),
                    cancellationToken);
                var contextJson = JsonSerializer.Serialize(contextPack, s_contextPackOpts);
                var prompt = $"<bishop-context>\n{contextJson}\n</bishop-context>\n\n/bish-auto-card #{card.Number}";
                var runResult = await _claude.RunPromptAsync(batch.WorktreePath, prompt, request.Model, card.Number, cancellationToken);

                Console.Out.WriteLine($"exit {runResult.ExitCode}");

                if (runResult.ExitCode == 0)
                {
                    var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
                    await _sender.Send(
                        new RecordClaudeRunCommand(card.Id, totals.InputTokens, totals.OutputTokens, totals.CacheCreationTokens, totals.CacheReadTokens),
                        cancellationToken);

                    var handoff = await ReadAndDeleteHandoffAsync(batch.WorktreePath, cancellationToken);

                    if (handoff is null)
                    {
                        await _git.ResetHardAsync(batch.WorktreePath, cancellationToken);
                        await _git.CleanWorkingTreeAsync(batch.WorktreePath, cancellationToken);
                        await _sender.Send(new RecordAutoRunFailureCommand(card.Id), cancellationToken);
                        failedNumbers.Add(card.Number);
                        return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.CardFailure);
                    }

                    var prefix = TagToPrefix(card.TagName);
                    var message = ComposeCommitMessage(prefix, card.Title, card.Number, handoff.CommitBodyBullets);

                    string commitHash;
                    try
                    {
                        await _git.StageAllAsync(batch.WorktreePath, cancellationToken);
                        commitHash = await _git.CommitAsync(batch.WorktreePath, message, cancellationToken);
                    }
                    catch
                    {
                        await _git.ResetHardAsync(batch.WorktreePath, cancellationToken);
                        await _git.CleanWorkingTreeAsync(batch.WorktreePath, cancellationToken);
                        await _sender.Send(new RecordAutoRunFailureCommand(card.Id), cancellationToken);
                        failedNumbers.Add(card.Number);
                        return new RunBatchResult(succeeded, ToNullableList(failedNumbers), RunBatchStopReason.CardFailure);
                    }

                    await _sender.Send(new SetCardCommitCommand(card.Id, commitHash, batch.BranchName), cancellationToken);
                    await _sender.Send(
                        new UpdateCardCommand(card.Id, null, null, false, null,
                            AppendDescription: handoff.Notes,
                            ToLaneName: SystemLaneNames.Done,
                            KeepOpen: true),
                        cancellationToken);

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

            await _batches.CloseAsync(batch.Id, BatchClosedReason.Finished, cancellationToken: cancellationToken);
            return new RunBatchResult(succeeded, null, RunBatchStopReason.Finished);
        }
        finally
        {
            DeleteLockFile(lockPath);
        }
    }

    private static string LockFilePath(string worktreePath, Guid batchId) =>
        Path.Combine(worktreePath, ".bishop", $"batch-{batchId}.lock");

    private static void WriteLockFile(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.WriteAllText(lockPath, $"{Environment.ProcessId}\t{DateTimeOffset.UtcNow:O}");
    }

    private static void RefreshLockFile(string lockPath)
    {
        try { File.WriteAllText(lockPath, $"{Environment.ProcessId}\t{DateTimeOffset.UtcNow:O}"); } catch { }
    }

    private static void DeleteLockFile(string lockPath)
    {
        try { File.Delete(lockPath); } catch { }
    }

    private static async Task<HandoffPayload?> ReadAndDeleteHandoffAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(worktreePath, ".bishop", "handoff.json");
        if (!File.Exists(path))
            return null;

        HandoffPayload? result = null;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            result = JsonSerializer.Deserialize<HandoffPayload>(json);
        }
        catch { }
        finally
        {
            try { File.Delete(path); } catch { }
        }

        return result;
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

    private static Workspace CreateBatchWorkspace(Workspace original, string worktreePath) =>
        new()
        {
            Id = original.Id,
            Name = original.Name,
            Path = worktreePath,
            GitHubRepo = original.GitHubRepo,
            NextCardNumber = original.NextCardNumber,
            Position = original.Position,
            IsRemoved = original.IsRemoved,
            RemovedAt = original.RemovedAt,
            CreatedAt = original.CreatedAt,
            UpdatedAt = original.UpdatedAt,
        };

    private static IReadOnlyList<int>? ToNullableList(List<int> list) => list.Count > 0 ? list : null;

    private static readonly JsonSerializerOptions s_contextPackOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };
}
