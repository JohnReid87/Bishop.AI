using Bishop.App.Lanes;
using Bishop.App.Tags;
using Bishop.App.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed class LaunchWorkspaceCommandHandler : IRequestHandler<LaunchWorkspaceCommand, bool>
{
    private readonly ITerminalLauncher _launcher;
    private readonly IWorkspaceContextSeeder _seeder;
    private readonly IDefaultTagSeeder _tagSeeder;
    private readonly ISystemLaneSeeder _laneSeed;

    public LaunchWorkspaceCommandHandler(
        ITerminalLauncher launcher,
        IWorkspaceContextSeeder seeder,
        IDefaultTagSeeder tagSeeder,
        ISystemLaneSeeder laneSeed)
    {
        _launcher = launcher;
        _seeder = seeder;
        _tagSeeder = tagSeeder;
        _laneSeed = laneSeed;
    }

    public async Task<bool> Handle(LaunchWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await _laneSeed.EnsureAsync(request.Path, cancellationToken);
        await _tagSeeder.EnsureAsync(request.Path, cancellationToken);
        await _seeder.SeedAsync(request.Path, cancellationToken);
        return _launcher.Launch(request.Path, null, request.Snap);
    }
}
