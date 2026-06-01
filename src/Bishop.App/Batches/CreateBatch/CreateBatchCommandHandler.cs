using System.Data;
using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.CreateBatch;

internal sealed class CreateBatchCommandHandler : IRequestHandler<CreateBatchCommand, CreateBatchResult>
{
    private readonly IGitCli _git;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public CreateBatchCommandHandler(IGitCli git, IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _git = git;
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task<CreateBatchResult> Handle(CreateBatchCommand request, CancellationToken cancellationToken)
    {
        var cardIds = await ResolveCardIdsAsync(request, cancellationToken);

        var baseBranch = request.BaseBranch
            ?? await _git.GetCurrentBranchAsync(request.WorkspacePath, cancellationToken);

        await _git.CreateWorktreeAsync(request.WorkspacePath, request.BranchName, baseBranch, request.WorktreePath, cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            BranchName = request.BranchName,
            BaseBranch = baseBranch,
            WorktreePath = request.WorktreePath,
            Model = request.Model,
            Status = BatchStatus.Open,
            CreatedAt = _timeProvider.GetUtcNow()
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var cardId in cardIds)
        {
            await using var txDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
            await using var tx = await txDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var batchInTx = await txDb.Batches.FirstOrDefaultAsync(b => b.Id == batch.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Batch {batch.Id} not found.");
            await BatchAssignment.AssignAsync(txDb, batchInTx, cardId, cancellationToken);
            await txDb.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        return new CreateBatchResult(batch, cardIds.Count);
    }

    private async Task<IReadOnlyList<Guid>> ResolveCardIdsAsync(CreateBatchCommand request, CancellationToken cancellationToken)
    {
        if (request.CardNumbers.Length == 0 && request.TagName is null && request.LaneName is null)
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Card> query = db.Cards.AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId);

        if (request.CardNumbers.Length > 0)
        {
            query = query.Where(c => request.CardNumbers.Contains(c.Number));
            var cards = await query.ToListAsync(cancellationToken);
            var foundNumbers = cards.Select(c => c.Number).ToHashSet();
            var missing = request.CardNumbers.Where(n => !foundNumbers.Contains(n)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"Card numbers not found: {string.Join(", ", missing.Select(n => $"#{n}"))}");
            return cards.Select(c => c.Id).ToList();
        }

        if (request.TagName is not null)
            query = query.Where(c => c.TagName == request.TagName);
        if (request.LaneName is not null)
            query = query.Where(c => c.LaneName == request.LaneName);

        return await query.Select(c => c.Id).ToListAsync(cancellationToken);
    }
}
