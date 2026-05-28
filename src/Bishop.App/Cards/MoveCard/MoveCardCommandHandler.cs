using Bishop.App.Services.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bishop.App.Cards.MoveCard;

public sealed class MoveCardCommandHandler : IRequestHandler<MoveCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGhCli _ghCli;
    private readonly ILogger<MoveCardCommandHandler> _logger;

    public MoveCardCommandHandler(
        IDbContextFactory<BishopDbContext> dbFactory,
        IGhCli ghCli,
        ILogger<MoveCardCommandHandler> logger)
    {
        _dbFactory = dbFactory;
        _ghCli = ghCli;
        _logger = logger;
    }

    public async Task<Card> Handle(MoveCardCommand request, CancellationToken cancellationToken)
    {
        if (!SystemLaneNames.All.Contains(request.ToLaneName))
            throw new InvalidOperationException($"Lane '{request.ToLaneName}' is not a system lane.");

        Card card;
        bool enteringDone;
        bool leavingDone;

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            card = await db.Cards
                .Include(c => c.Workspace)
                .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
                ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

            if (request.ExpectedSourceLaneName is { } expectedLaneName)
            {
                if (card.LaneName != expectedLaneName)
                    throw new InvalidOperationException(
                        $"Card {request.CardId} was expected in lane '{expectedLaneName}' but is in lane '{card.LaneName}'.");
            }

            var sourceLaneName = card.LaneName;
            var workspaceId = card.WorkspaceId;
            var movingAcrossLanes = sourceLaneName != request.ToLaneName;

            enteringDone = false;
            leavingDone = false;

            if (movingAcrossLanes)
            {
                var sourceDone = sourceLaneName == SystemLaneNames.Done;
                var targetDone = request.ToLaneName == SystemLaneNames.Done;
                enteringDone = targetDone && !sourceDone;
                leavingDone = sourceDone && !targetDone;

                // Renumber remaining cards in the source lane.
                var sourceCards = await db.Cards
                    .Where(c => c.WorkspaceId == workspaceId && c.LaneName == sourceLaneName && c.Id != card.Id)
                    .OrderBy(c => c.Position)
                    .ToListAsync(cancellationToken);
                for (var i = 0; i < sourceCards.Count; i++)
                    sourceCards[i].Position = i + 1;
            }

            // Load target lane cards (excluding the card being moved if same lane).
            var targetCards = await db.Cards
                .Where(c => c.WorkspaceId == workspaceId && c.LaneName == request.ToLaneName && c.Id != card.Id)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);

            // Insert at the requested position (1-based, clamped to valid range).
            var insertAt = Math.Clamp(request.ToPosition - 1, 0, targetCards.Count);
            targetCards.Insert(insertAt, card);

            card.LaneName = request.ToLaneName;
            for (var i = 0; i < targetCards.Count; i++)
                targetCards[i].Position = i + 1;

            // Inline close/reopen atomically with the move so both writes are in
            // one SaveChangesAsync call and share the same transaction.
            if (enteringDone && !request.KeepOpen)
                card.IsClosed = true;
            else if (leavingDone)
                card.IsClosed = false;

            await db.SaveChangesAsync(cancellationToken);
        }

        // GitHub side-effect — best-effort after the atomic DB commit.
        // A failure here is logged but does not roll back the (consistent) DB state.
        if (enteringDone && !request.KeepOpen
            && card.GitHubIssueNumber.HasValue
            && card.Workspace.GitHubRepo is { } closeRepo)
        {
            try
            {
                await _ghCli.RunAsync(
                    ["issue", "close", card.GitHubIssueNumber.ToString()!, "--repo", closeRepo],
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "GitHub issue close failed for card {CardId} (issue #{Issue}); DB state is consistent.",
                    card.Id, card.GitHubIssueNumber);
            }
        }
        else if (leavingDone
            && card.GitHubIssueNumber.HasValue
            && card.Workspace.GitHubRepo is { } reopenRepo)
        {
            try
            {
                await _ghCli.RunAsync(
                    ["issue", "reopen", card.GitHubIssueNumber.ToString()!, "--repo", reopenRepo],
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "GitHub issue reopen failed for card {CardId} (issue #{Issue}); DB state is consistent.",
                    card.Id, card.GitHubIssueNumber);
            }
        }

        return card;
    }
}
