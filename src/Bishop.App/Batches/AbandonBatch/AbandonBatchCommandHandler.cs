using Bishop.App.Cards.MoveCard;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.AbandonBatch;

public sealed class AbandonBatchCommandHandler : IRequestHandler<AbandonBatchCommand, AbandonBatchResult>
{
    private readonly IGitCli _git;
    private readonly ISender _sender;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AbandonBatchCommandHandler(
        IGitCli git,
        ISender sender,
        IDbContextFactory<BishopDbContext> dbFactory)
    {
        _git = git;
        _sender = sender;
        _dbFactory = dbFactory;
    }

    public async Task<AbandonBatchResult> Handle(AbandonBatchCommand request, CancellationToken cancellationToken)
    {
        await using var readDb = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var matches = await readDb.Batches.AsNoTracking().ByName(request.Name).ToListAsync(cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];
        if (batch.Status == BatchStatus.Closed)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' is already Closed and cannot be abandoned.");

        var cards = await readDb.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        foreach (var card in cards)
            await _sender.Send(new MoveCardCommand(card.Id, SystemLaneNames.ToDo, 1), cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var trackedCards = await db.Cards.Where(c => c.BatchId == batch.Id).ToListAsync(cancellationToken);
        foreach (var card in trackedCards)
            card.BatchId = null;

        var batchToClose = await db.Batches.FirstOrDefaultAsync(b => b.Id == batch.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batch.Id} not found.");
        batchToClose.Close(BatchClosedReason.Abandoned, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        await _git.RemoveWorktreeAsync(request.WorkspacePath, batch.WorktreePath, cancellationToken);

        return new AbandonBatchResult(cards.Count);
    }
}
