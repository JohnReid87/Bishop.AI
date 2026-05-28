using Bishop.App.Cards.MoveCard;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.AbandonBatch;

public sealed class AbandonBatchCommandHandler : IRequestHandler<AbandonBatchCommand, AbandonBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IGitCli _git;
    private readonly ISender _sender;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public AbandonBatchCommandHandler(
        IBatchRepository batches,
        IGitCli git,
        ISender sender,
        IDbContextFactory<BishopDbContext> dbFactory)
    {
        _batches = batches;
        _git = git;
        _sender = sender;
        _dbFactory = dbFactory;
    }

    public async Task<AbandonBatchResult> Handle(AbandonBatchCommand request, CancellationToken cancellationToken)
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
                $"Batch '{request.Name}' must be Working to abandon; current status is {batch.Status}.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        foreach (var card in cards)
            await _sender.Send(new MoveCardCommand(card.Id, SystemLaneNames.ToDo, 1), cancellationToken);

        foreach (var card in cards)
            await _batches.UnassignCardAsync(batch.Id, card.Id, cancellationToken);

        await _batches.CloseAsync(batch.Id, BatchClosedReason.Abandoned, cancellationToken: cancellationToken);
        await _git.RemoveWorktreeAsync(request.WorkspacePath, batch.WorktreePath, cancellationToken);

        return new AbandonBatchResult(cards.Count);
    }
}
