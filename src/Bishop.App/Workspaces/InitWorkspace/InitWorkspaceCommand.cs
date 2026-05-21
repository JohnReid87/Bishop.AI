using MediatR;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed record InitWorkspaceCommand(
    string Path,
    string? Name = null,
    bool SeedTags = true,
    bool DetectGitHub = true) : IRequest<InitWorkspaceResult>;
