using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.SetCardCommit;
using Bishop.App.Cards.UpdateCard;
using Bishop.Core;
using Bishop.App.Cards.RecordAutoRunFailure;
using Bishop.App.Cards.RecordClaudeRun;
using Bishop.App.Services.Claude;
using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Skills.GetSkillBootstrapInfo;
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

                var context = await BuildAutoCardContextAsync(request.WorkspaceId, request.WorkspacePath, card, cancellationToken);
                var prompt = $"{context.FormatAsBlock()}\n\n/bish-auto-card #{card.Number}";
                var claudeSw = Stopwatch.StartNew();
                var runResult = await _claude.RunPromptAsync(request.WorkspacePath, prompt, request.Model, card.Number, cancellationToken);
                claudeSw.Stop();

                var recordElapsed = TimeSpan.Zero;
                var cardFailed = false;

                if (runResult.ExitCode == 0)
                {
                    var handoffPath = Path.Combine(bishopDir, "handoff.json");
                    HandoffPayload? handoff = null;
                    if (File.Exists(handoffPath))
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(handoffPath, cancellationToken);
                            handoff = JsonSerializer.Deserialize<HandoffPayload>(json, HandoffSerializerOptions);
                        }
                        catch { }
                        finally
                        {
                            try { File.Delete(handoffPath); } catch { }
                        }
                    }

                    if (handoff is not null)
                    {
                        var prefix = TagToPrefix(card.TagName);
                        var commitMessage = ComposeCommitMessage(prefix, card.Title, card.Number, handoff.CommitBodyBullets);

                        string? commitHash = null;
                        string? branch = null;
                        try
                        {
                            await _git.StageAllAsync(request.WorkspacePath, cancellationToken);
                            commitHash = await _git.CommitAsync(request.WorkspacePath, commitMessage, cancellationToken);
                            branch = await _git.GetCurrentBranchAsync(request.WorkspacePath, cancellationToken);
                        }
                        catch
                        {
                            await _git.ResetHardAsync(request.WorkspacePath, cancellationToken);
                            await _git.CleanWorkingTreeAsync(request.WorkspacePath, cancellationToken);
                            await _sender.Send(new RecordAutoRunFailureCommand(card.Id), cancellationToken);
                            failedCardNumbers.Add(card.Number);
                            cardFailed = true;
                        }

                        if (!cardFailed)
                        {
                            var totals = runResult.Totals ?? new ClaudeRunTotals(0, 0);
                            var recordSw = Stopwatch.StartNew();
                            await _sender.Send(
                                new RecordClaudeRunCommand(card.Id, totals.InputTokens, totals.OutputTokens, totals.CacheCreationTokens, totals.CacheReadTokens),
                                cancellationToken);
                            recordSw.Stop();
                            recordElapsed = recordSw.Elapsed;

                            try
                            {
                                await _sender.Send(new SetCardCommitCommand(card.Id, commitHash!, branch!), cancellationToken);
                            }
                            catch { }

                            await _sender.Send(
                                new UpdateCardCommand(card.Id, null, null, false, null,
                                    AppendDescription: handoff.Notes,
                                    ToLaneName: SystemLaneNames.Done,
                                    KeepOpen: true),
                                cancellationToken);

                            succeeded++;
                        }
                    }
                    else
                    {
                        await _git.ResetHardAsync(request.WorkspacePath, cancellationToken);
                        await _git.CleanWorkingTreeAsync(request.WorkspacePath, cancellationToken);
                        await _sender.Send(new RecordAutoRunFailureCommand(card.Id), cancellationToken);
                        failedCardNumbers.Add(card.Number);
                        cardFailed = true;
                    }
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

                if (cardFailed)
                    continue;

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

    private static readonly JsonSerializerOptions HandoffSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static string TagToPrefix(string? tag) => tag switch
    {
        "feature" => "feat",
        "bug" => "fix",
        "chore" => "chore",
        "docs" => "docs",
        "refactor" => "refactor",
        "test" => "test",
        _ => "chore",
    };

    private static string ComposeCommitMessage(string prefix, string title, int cardNumber, IReadOnlyList<string> bullets)
    {
        var subject = $"{prefix}: {title} (card {cardNumber})";
        if (bullets.Count == 0)
            return subject;
        var body = string.Join("\n", bullets);
        return $"{subject}\n\n{body}";
    }

    private static readonly Regex RelatedCardPattern = new(@"#(\d+)", RegexOptions.Compiled);

    private async Task<AutoCardPromptContext> BuildAutoCardContextAsync(
        Guid workspaceId,
        string workspacePath,
        Card card,
        CancellationToken cancellationToken)
    {
        var bootstrapTask = _sender.Send(new GetSkillBootstrapInfoQuery(workspaceId), cancellationToken);
        var commitsTask = _git.GetRecentCommitsAsync(workspacePath, cancellationToken);

        await Task.WhenAll(bootstrapTask, commitsTask);

        var bootstrap = await bootstrapTask;
        var commitsResult = await commitsTask;

        var recentCommits = commitsResult is GetRecentCommitsResult.Success s
            ? s.Commits.Take(20).Select(c => new AutoCardCommitSummary(c.ShortHash, c.Subject)).ToList()
            : new List<AutoCardCommitSummary>();

        var relatedNumbers = ParseRelatedCardNumbers(card.Description);
        var relatedCards = new List<AutoCardPromptCard>();
        foreach (var num in relatedNumbers)
        {
            var related = await _sender.Send(new GetCardByNumberQuery(num, workspaceId), cancellationToken);
            if (related is not null)
                relatedCards.Add(ToCardContext(related));
        }

        return new AutoCardPromptContext(bootstrap, ToCardContext(card), recentCommits, relatedCards);
    }

    private static AutoCardPromptCard ToCardContext(Card card) =>
        new(card.Number, card.Title, card.Description, card.LaneName, card.TagName, card.IsClosed);

    private static IReadOnlyList<int> ParseRelatedCardNumbers(string description)
    {
        var idx = description.IndexOf("### Related", StringComparison.Ordinal);
        if (idx < 0) return [];

        var section = description[idx..];
        var nextSection = section.IndexOf("\n### ", 1, StringComparison.Ordinal);
        if (nextSection > 0) section = section[..nextSection];

        return RelatedCardPattern.Matches(section)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .ToList();
    }

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
