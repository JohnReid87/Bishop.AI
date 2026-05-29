using Bishop.App.Git;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Batches.GetBatchPruneCandidates;

public sealed class GetBatchPruneCandidatesQueryHandler
    : IRequestHandler<GetBatchPruneCandidatesQuery, IReadOnlyList<PruneBatchCandidate>>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGitCli _git;

    public GetBatchPruneCandidatesQueryHandler(IDbContextFactory<BishopDbContext> dbFactory, IGitCli git)
    {
        _dbFactory = dbFactory;
        _git = git;
    }

    public async Task<IReadOnlyList<PruneBatchCandidate>> Handle(
        GetBatchPruneCandidatesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var allBatches = (await db.Batches.AsNoTracking()
            .ByWorkspace(request.WorkspaceId)
            .ToListAsync(cancellationToken))
            .OrderBy(b => b.CreatedAt)
            .ToList();

        IEnumerable<Batch> closed = allBatches.Where(b => b.Status == BatchStatus.Closed && b.ClosedAt.HasValue);

        if (request.AbandonedOnly)
            closed = closed.Where(b => b.ClosedReason == BatchClosedReason.Abandoned);
        if (request.MergedOnly)
            closed = closed.Where(b => b.ClosedReason == BatchClosedReason.Finished);
        if (request.OlderThan is { } olderThan)
            closed = closed.Where(b => DateTimeOffset.UtcNow - b.ClosedAt!.Value >= olderThan);

        var checkedOutBranches = await _git.GetWorktreeBranchesAsync(request.WorkspacePath, cancellationToken);
        var checkedOutSet = new HashSet<string>(checkedOutBranches, StringComparer.OrdinalIgnoreCase);

        var candidates = new List<PruneBatchCandidate>();
        foreach (var batch in closed)
        {
            if (!await _git.LocalBranchExistsAsync(request.WorkspacePath, batch.BranchName, cancellationToken))
                continue;

            var commitCount = await _git.GetBranchCommitCountAsync(
                request.WorkspacePath, batch.BranchName, batch.BaseBranch, cancellationToken) ?? 0;
            var isCheckedOut = checkedOutSet.Contains(batch.BranchName);

            candidates.Add(new PruneBatchCandidate(
                batch.Name,
                batch.BranchName,
                batch.ClosedReason!.Value,
                batch.ClosedAt!.Value,
                commitCount,
                isCheckedOut));
        }

        return candidates;
    }
}
