using MediatR;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed record InitWorkspaceCommand(
    string Path,
    string? Name = null,
    bool DetectGitHub = true,
    InitWorkspaceArchivedAction? ArchivedAction = null) : IRequest<InitWorkspaceResult>;
