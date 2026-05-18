using MediatR;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed record LaunchWorkspaceCommand(string Path) : IRequest;
