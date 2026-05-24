using Bishop.App.Git;
using Bishop.App.Services.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.FinishBatch;

public sealed class FinishBatchCommandHandler : IRequestHandler<FinishBatchCommand, FinishBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IGitCli _git;
    private readonly IGhCli _ghCli;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public FinishBatchCommandHandler(
        IBatchRepository batches,
        IGitCli git,
        IGhCli ghCli,
        IDbContextFactory<BishopDbContext> dbFactory)
    {
        _batches = batches;
        _git = git;
        _ghCli = ghCli;
        _dbFactory = dbFactory;
    }

    public async Task<FinishBatchResult> Handle(FinishBatchCommand request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];
        if (batch.Status != BatchStatus.Working)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' must be Working to finish; current status is {batch.Status}.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var pushResult = await _git.PushAsync(batch.WorktreePath, cancellationToken);
        if (!pushResult.Success)
            throw new InvalidOperationException(
                $"Failed to push branch '{batch.BranchName}': {pushResult.Message}");

        var bodyLines = cards.Select(c =>
        {
            var shortHash = c.CommitHash is { Length: >= 7 } h ? $" — {h[..7]}" : string.Empty;
            return $"- [x] #{c.Number} {c.Title}{shortHash}";
        });
        var body = string.Join("\n", bodyLines);

        var prUrl = await _ghCli.RunCaptureAsync(
            ["pr", "create",
             "--repo", request.GitHubRepo,
             "--title", batch.Name,
             "--body", body,
             "--head", batch.BranchName,
             "--base", batch.BaseBranch],
            cancellationToken);

        await _batches.CloseAsync(batch.Id, BatchClosedReason.Finished, cancellationToken);
        await _git.RemoveWorktreeAsync(request.WorkspacePath, batch.WorktreePath, cancellationToken);

        return new FinishBatchResult(prUrl.Trim());
    }
}
