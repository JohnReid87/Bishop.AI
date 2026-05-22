using Bishop.App.Tags;
using Bishop.App.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed class LaunchWorkspaceCommandHandler : IRequestHandler<LaunchWorkspaceCommand, bool>
{
    private readonly ITerminalLauncher _launcher;
    private readonly IWorkspaceContextSeeder _seeder;
    private readonly IDefaultTagSeeder _tagSeeder;

    public LaunchWorkspaceCommandHandler(
        ITerminalLauncher launcher,
        IWorkspaceContextSeeder seeder,
        IDefaultTagSeeder tagSeeder)
    {
        _launcher = launcher;
        _seeder = seeder;
        _tagSeeder = tagSeeder;
    }

    public async Task<bool> Handle(LaunchWorkspaceCommand request, CancellationToken cancellationToken)
    {
        await _tagSeeder.EnsureAsync(request.Path, cancellationToken);
        await _seeder.SeedAsync(request.Path, cancellationToken);
        return _launcher.Launch(request.Path, null, request.Snap);
    }
}
