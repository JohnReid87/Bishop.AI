namespace Bishop.App.Context.ContextPack;

public sealed record ContextPack(
    WorkspaceBlock Workspace,
    GitBlock Git,
    object? SkillSpecific,
    IReadOnlyDictionary<string, string> Conventions);

public sealed record WorkspaceBlock(
    string Name,
    string Path,
    string? GitHubRepo,
    IReadOnlyList<string> Lanes,
    IReadOnlyList<string> Tags,
    string? ContextMd,
    bool ContextMdTruncated);

public sealed record GitBlock(
    string? Branch,
    IReadOnlyList<CommitSummary> Commits);

public sealed record CommitSummary(
    string ShortHash,
    string Subject,
    DateTimeOffset Timestamp);
