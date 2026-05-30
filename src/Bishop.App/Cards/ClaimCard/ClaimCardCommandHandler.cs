using System.Data;
using Bishop.App.Cards.GetCard;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ClaimCard;

public sealed class ClaimCardCommandHandler : IRequestHandler<ClaimCardCommand, Card?>
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromMilliseconds(50);

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly ISender _sender;

    public ClaimCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, ISender sender)
    {
        _dbFactory = dbFactory;
        _sender = sender;
    }

    public async Task<Card?> Handle(ClaimCardCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var claimedCardId = await TryClaimAsync(request, cancellationToken);
                return claimedCardId is null
                    ? null
                    : await _sender.Send(new GetCardQuery(claimedCardId.Value), cancellationToken);
            }
            catch (Exception ex) when (IsRetryableSqlite(ex) && attempt < MaxAttempts)
            {
                // Stryker disable once Statement: backoff is observable only via wall-clock timing; removing the await is behaviourally equivalent for unit tests.
                await Task.Delay(RetryBackoff, cancellationToken);
            }
        }

        return null;
    }

    private async Task<Guid?> TryClaimAsync(ClaimCardCommand request, CancellationToken cancellationToken)
    {
        // Serializable on SQLite issues BEGIN IMMEDIATE — acquires a RESERVED lock up
        // front so two concurrent claims serialize instead of both reading the same top.
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var sourceLaneName = SystemLaneNames.All
            .FirstOrDefault(n => string.Equals(n, request.SourceLaneName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Lane '{request.SourceLaneName}' is not a system lane.");

        var workspaceId = request.WorkspaceId;
        var query = db.Cards.Where(c => c.WorkspaceId == workspaceId && c.LaneName == sourceLaneName);

        if (!string.IsNullOrEmpty(request.TagName))
        {
            var tagName = request.TagName;
            query = query.Where(c => c.TagName == tagName);
        }

        var topCard = await query
            .OrderBy(c => c.Position)
            .FirstOrDefaultAsync(cancellationToken);

        if (topCard is null)
        {
            // Stryker disable once Statement: empty-read transaction with no writes — commit vs auto-rollback on dispose is indistinguishable to any observer.
            await tx.CommitAsync(cancellationToken);
            return null;
        }

        // Inline move: source lane → Doing at position 1. Doing is never the auto-close
        // lane (only Done is), so no Close/Reopen dispatch is required.
        var sourceSiblings = await db.Cards
            .Where(c => c.WorkspaceId == workspaceId && c.LaneName == sourceLaneName && c.Id != topCard.Id)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);
        for (var i = 0; i < sourceSiblings.Count; i++)
            sourceSiblings[i].Position = i + 1;

        var doingLaneName = SystemLaneNames.Doing;
        var doingCards = await db.Cards
            .Where(c => c.WorkspaceId == workspaceId && c.LaneName == doingLaneName && c.Id != topCard.Id)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);

        topCard.LaneName = doingLaneName;
        doingCards.Insert(0, topCard);
        for (var i = 0; i < doingCards.Count; i++)
            doingCards[i].Position = i + 1;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return topCard.Id;
    }

    private static bool IsRetryableSqlite(Exception ex)
    {
        var sqlite = ex as SqliteException ?? ex.InnerException as SqliteException;
        if (sqlite is null)
            return false;
        return sqlite.SqliteErrorCode is 5 /* SQLITE_BUSY */ or 6 /* SQLITE_LOCKED */;
    }
}
