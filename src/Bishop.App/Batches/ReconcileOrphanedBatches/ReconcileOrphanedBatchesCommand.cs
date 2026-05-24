using MediatR;

namespace Bishop.App.Batches.ReconcileOrphanedBatches;

public sealed record ReconcileOrphanedBatchesCommand : IRequest;
