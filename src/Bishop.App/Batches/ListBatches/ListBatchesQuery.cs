using MediatR;

namespace Bishop.App.Batches.ListBatches;

public sealed record ListBatchesQuery : IRequest<IReadOnlyList<BatchSummary>>;
