using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.CreateBatch;

public sealed class CreateBatchCommandHandler : IRequestHandler<CreateBatchCommand, CreateBatchResult>
{
    private readonly IBatchRepository _batches;
    private readonly IGitCli _git;
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public CreateBatchCommandHandler(IBatchRepository batches, IGitCli git, IDbContextFactory<BishopDbContext> dbFactory)
    {
        _batches = batches;
        _git = git;
        _dbFactory = dbFactory;
    }

    public async Task<CreateBatchResult> Handle(CreateBatchCommand request, CancellationToken cancellationToken)
    {
        var cardIds = await ResolveCardIdsAsync(request, cancellationToken);

        var baseBranch = request.BaseBranch
            ?? await _git.GetCurrentBranchAsync(request.WorkspacePath, cancellationToken);

        await _git.CreateWorktreeAsync(request.WorkspacePath, request.BranchName, baseBranch, request.WorktreePath, cancellationToken);

        var batch = await _batches.CreateAsync(request.WorkspaceId, request.Name, request.BranchName, baseBranch, request.WorktreePath, cancellationToken);

        foreach (var cardId in cardIds)
            await _batches.AssignCardAsync(batch.Id, cardId, cancellationToken);

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
