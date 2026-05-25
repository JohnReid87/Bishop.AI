using Bishop.App.Cards.CloseCard;
using Bishop.App.Git;
using Bishop.App.Services.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bishop.App.Batches.CompleteBatch;

public sealed class CompleteBatchCommandHandler : IRequestHandler<CompleteBatchCommand>
{
    private readonly IBatchRepository _batches;
    private readonly IGhCli _ghCli;
    private readonly IGitCli _git;
    private readonly ISender _sender;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ILogger<CompleteBatchCommandHandler> _logger;

    public CompleteBatchCommandHandler(
        IBatchRepository batches,
        IGhCli ghCli,
        IGitCli git,
        ISender sender,
        IDbContextFactory<BishopDbContext> dbFactory,
        ILogger<CompleteBatchCommandHandler> logger)
    {
        _batches = batches;
        _ghCli = ghCli;
        _git = git;
        _sender = sender;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Handle(CompleteBatchCommand request, CancellationToken cancellationToken)
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
                $"Batch '{request.Name}' must be Working to complete; current status is {batch.Status}.");
        if (batch.GitHubPrUrl is null)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' has no PR URL. Run 'bishop batch finish' first.");

        // Merge PR — throws with gh stderr on non-zero exit; no state change on failure
        await _ghCli.RunCaptureAsync(
            ["pr", "merge", batch.GitHubPrUrl, "--squash", "--delete-branch"],
            cancellationToken);

        // Close every card already in Done
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var doneCards = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id && c.LaneName == SystemLaneNames.Done)
            .ToListAsync(cancellationToken);

        foreach (var card in doneCards)
            await _sender.Send(new CloseCardCommand(card.Id), cancellationToken);

        await _batches.CloseAsync(batch.Id, BatchClosedReason.Finished, batch.GitHubPrUrl, cancellationToken);

        // Best-effort local branch cleanup
        try
        {
            await _git.DeleteLocalBranchAsync(request.WorkspacePath, batch.BranchName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete local branch '{Branch}' after completing batch '{Name}'.",
                batch.BranchName, batch.Name);
        }
    }
}
