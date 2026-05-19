namespace Bishop.App.Git;

public sealed record CommitInfo(string ShortHash, string Subject, DateTimeOffset Timestamp);
