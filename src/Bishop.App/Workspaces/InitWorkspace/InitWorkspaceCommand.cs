using MediatR;

namespace Bishop.App.Workspaces.InitWorkspace;

public sealed record InitWorkspaceCommand(string Path, string? Name = null) : IRequest<InitWorkspaceResult>;
