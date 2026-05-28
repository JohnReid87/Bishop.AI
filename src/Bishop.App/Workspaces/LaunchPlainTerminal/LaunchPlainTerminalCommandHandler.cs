using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchPlainTerminal;

public sealed class LaunchPlainTerminalCommandHandler : IRequestHandler<LaunchPlainTerminalCommand, bool>
{
    private readonly ITerminalLauncher _launcher;

    public LaunchPlainTerminalCommandHandler(ITerminalLauncher launcher) => _launcher = launcher;

    public Task<bool> Handle(LaunchPlainTerminalCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(_launcher.LaunchPlain(request.Path, request.Snap));
}
