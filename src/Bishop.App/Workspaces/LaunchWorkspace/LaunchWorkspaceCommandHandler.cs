using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchWorkspace;

internal sealed class LaunchWorkspaceCommandHandler : IRequestHandler<LaunchWorkspaceCommand, bool>
{
    private readonly ITerminalLauncher _launcher;
    private readonly IWorkspaceContextSeeder _seeder;

    public LaunchWorkspaceCommandHandler(ITerminalLauncher launcher, IWorkspaceContextSeeder seeder)
    {
        _launcher = launcher;
        _seeder = seeder;
    }

    public async Task<bool> Handle(LaunchWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await _seeder.SeedAsync(request.Path, cancellationToken);
        return _launcher.Launch(request.Path, null, request.Snap);
    }
}
