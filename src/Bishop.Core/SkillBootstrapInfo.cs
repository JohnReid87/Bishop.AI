namespace Bishop.Core;

public sealed record SkillBootstrapInfo(
    string WorkspaceName,
    string WorkspacePath,
    IReadOnlyList<TagInfo> Tags,
    IReadOnlyList<LaneInfo> Lanes);
