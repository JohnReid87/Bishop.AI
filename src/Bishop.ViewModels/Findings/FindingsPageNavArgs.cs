namespace Bishop.ViewModels.Findings;

public sealed record FindingsPageNavArgs(
    Guid WorkspaceId,
    string WorkspacePath,
    string? GitHubRepo,
    string SkillName,
    string? ProjectName);
