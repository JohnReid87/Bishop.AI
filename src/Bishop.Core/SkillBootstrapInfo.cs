namespace Bishop.Core;

public sealed record SkillBootstrapInfo(
    string WorkspaceName,
    string WorkspacePath,
    string? GitHubRepo,
    IReadOnlyList<TagInfo> Tags,
    IReadOnlyList<LaneInfo> Lanes);
