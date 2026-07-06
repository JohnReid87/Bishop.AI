using MediatR;

namespace Bishop.App.Batches.ListBatches;

public sealed record ListBatchesQuery(Guid WorkspaceId, string WorkspacePath, bool IncludeClosed = false) : IRequest<IReadOnlyList<BatchSummary>>;
