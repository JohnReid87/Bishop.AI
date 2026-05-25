using Bishop.Core;

namespace Bishop.Data;

public interface IBatchRepository
{
    Task<Batch> CreateAsync(string name, string branchName, string baseBranch, string worktreePath, CancellationToken cancellationToken = default);
    Task<Batch?> GetAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<Batch?> GetByBranchNameAsync(string branchName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> ListAsync(CancellationToken cancellationToken = default);
    Task<Batch> TransitionToWorkingAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<Batch> SetGitHubPrUrlAsync(Guid batchId, string prUrl, CancellationToken cancellationToken = default);
    Task<Batch> CloseAsync(Guid batchId, BatchClosedReason reason, string? prUrl = null, CancellationToken cancellationToken = default);
    Task AssignCardAsync(Guid batchId, Guid cardId, CancellationToken cancellationToken = default);
    Task UnassignCardAsync(Guid batchId, Guid cardId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid batchId, CancellationToken cancellationToken = default);
}
