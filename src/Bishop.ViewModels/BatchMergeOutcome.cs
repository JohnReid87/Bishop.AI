namespace Bishop.ViewModels;

public sealed record BatchMergeOutcome(
    bool Success,
    IReadOnlyList<string> ConflictFiles,
    string? ErrorMessage);
