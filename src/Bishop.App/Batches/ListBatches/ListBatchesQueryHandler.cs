using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.ListBatches;

public sealed class ListBatchesQueryHandler : IRequestHandler<ListBatchesQuery, IReadOnlyList<BatchSummary>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGitCli _git;

    public ListBatchesQueryHandler(IDbContextFactory<BishopDbContext> dbFactory, IGitCli git)
    {
        _dbFactory = dbFactory;
        _git = git;
    }

    public async Task<IReadOnlyList<BatchSummary>> Handle(ListBatchesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var all = (await db.Batches.AsNoTracking()
            .ByWorkspace(request.WorkspaceId)
            .ToListAsync(cancellationToken))
            .OrderBy(b => b.CreatedAt)
            .ToList();

        if (all.Count == 0)
            return [];

        var batchIds = all.Select(b => b.Id).ToList();

        var allCards = await db.Cards.AsNoTracking()
            .Where(c => c.BatchId.HasValue && batchIds.Contains(c.BatchId!.Value))
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var cardsByBatch = allCards
            .GroupBy(c => c.BatchId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Card>)g.ToList());

        var gitStateTasks = all.Select(async b =>
        {
            var branchExistsTask = _git.LocalBranchExistsAsync(request.WorkspacePath, b.BranchName, cancellationToken);
            var isMergedTask = _git.IsBranchMergedIntoAsync(request.WorkspacePath, b.BranchName, b.BaseBranch, cancellationToken);
            var worktreeExists = !string.IsNullOrEmpty(b.WorktreePath) && Directory.Exists(b.WorktreePath);
            await Task.WhenAll(branchExistsTask, isMergedTask);
            return (b.Id, IsMerged: isMergedTask.Result, BranchExists: branchExistsTask.Result, WorktreeExists: worktreeExists);
        }).ToList();

        await Task.WhenAll(gitStateTasks);

        var gitStates = gitStateTasks.Select(t => t.Result).ToDictionary(x => x.Id);

        return all.Select(b =>
        {
            var gs = gitStates[b.Id];
            var cards = cardsByBatch.GetValueOrDefault(b.Id, []);
            return new BatchSummary(b, cards.Count, b.FinishedAt, gs.IsMerged, gs.BranchExists, gs.WorktreeExists, cards);
        }).ToList();
    }
}
