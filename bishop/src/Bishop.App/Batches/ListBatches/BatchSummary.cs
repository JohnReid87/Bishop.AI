using Bishop.Core;

namespace Bishop.App.Batches.ListBatches;

public sealed record BatchSummary(Batch Batch, int CardCount, DateTimeOffset? FinishedAt, DateTimeOffset? MergedAt, bool IsMerged, bool BranchExists, bool WorktreeExists, IReadOnlyList<Card> Cards);
