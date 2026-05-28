namespace Bishop.App.Batches.MergeBatch;

public sealed record MergeBatchResult(bool Success, IReadOnlyList<string> ConflictFiles, string? ErrorMessage = null);
