namespace Bishop.App.Git;

public sealed record MergeResult(bool Success, IReadOnlyList<string> ConflictFiles, string? ErrorMessage = null);
