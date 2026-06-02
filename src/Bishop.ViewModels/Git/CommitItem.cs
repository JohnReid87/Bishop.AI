namespace Bishop.ViewModels.Git;

public sealed record CommitItem(
    string ShortHash,
    string FullHash,
    string Subject,
    string Body,
    DateTimeOffset Timestamp,
    bool IsPushed);
