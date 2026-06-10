using Bishop.Core;

namespace Bishop.App.Batches.GetBatch;

public sealed record GetBatchResult(Batch Batch, IReadOnlyList<Card> Cards);
