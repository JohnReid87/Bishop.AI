namespace Bishop.ViewModels.GitHub;

public sealed record CommitItem(
    string ShortHash,
    string FullHash,
    string Subject,
    string Body,
    DateTimeOffset Timestamp,
    bool IsPushed);
