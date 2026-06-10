using Bishop.Core;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches;

internal static class BatchAssignment
{
    // Precondition: caller has opened a Serializable transaction on db.
    // Caller owns SaveChangesAsync + Commit.
    internal static async Task AssignAsync(
        BishopDbContext db, Batch batch, Guid cardId, CancellationToken ct)
    {
        if (batch.Status == BatchStatus.Closed)
            throw new InvalidOperationException($"Cannot assign a card to a Closed batch.");

        var card = await db.Cards
            .Include(c => c.Batch)
            .FirstOrDefaultAsync(c => c.Id == cardId, ct)
            ?? throw new InvalidOperationException($"Card {cardId} not found.");

        if (card.BatchId is Guid owner && owner != batch.Id && card.Batch!.Status != BatchStatus.Closed)
            throw new InvalidOperationException(
                $"Card {cardId} is already assigned to batch {card.BatchId} which is not Closed.");

        card.BatchId = batch.Id;
    }
}
