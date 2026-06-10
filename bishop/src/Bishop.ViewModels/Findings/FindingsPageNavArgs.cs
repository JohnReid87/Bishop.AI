using Bishop.ViewModels.Workspaces;

namespace Bishop.ViewModels.Findings;

public sealed record FindingsPageNavArgs(
    Guid WorkspaceId,
    string WorkspacePath,
    string SkillName,
    string? ProjectName,
    WorkspaceItemViewModel? Workspace = null,
    WorkspaceTab SourceTab = WorkspaceTab.Monitoring);
