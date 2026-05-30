namespace Bishop.ViewModels.Batches;

public sealed record BatchMergeOutcome(
    bool Success,
    IReadOnlyList<string> ConflictFiles,
    string? ErrorMessage);
