namespace Bishop.App.Git;

public sealed record CommitInfo(string ShortHash, string FullHash, string Subject, string Body, DateTimeOffset Timestamp);
