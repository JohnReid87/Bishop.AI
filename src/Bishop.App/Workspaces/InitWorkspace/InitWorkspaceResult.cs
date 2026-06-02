using Bishop.Core;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed record InitWorkspaceResult(
    Workspace Workspace,
    bool Created,
    bool Restored = false,
    bool NeedsArchivedAction = false);
