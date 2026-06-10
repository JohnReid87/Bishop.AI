namespace Bishop.App.Batches.CleanUpBatch;

public sealed record CleanUpBatchResult(IReadOnlyList<int> ClosedCardNumbers);
