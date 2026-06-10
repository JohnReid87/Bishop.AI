using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed record LaunchWorkspaceCommand(string Path, TerminalSnap? Snap = null) : IRequest<bool>;
