using Bishop.App.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed class LaunchWorkspaceCommandHandler : IRequestHandler<LaunchWorkspaceCommand, bool>
{
    private readonly ITerminalLauncher _launcher;

    public LaunchWorkspaceCommandHandler(ITerminalLauncher launcher) => _launcher = launcher;

    public Task<bool> Handle(LaunchWorkspaceCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(_launcher.Launch(request.Path, null, request.Snap));
}
