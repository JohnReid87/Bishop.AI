using Bishop.App.Terminal;
using MediatR;

namespace Bishop.App.Skills.LaunchSkill;

public sealed class LaunchSkillCommandHandler : IRequestHandler<LaunchSkillCommand, bool>
{
    private readonly ITerminalLauncher _launcher;
    private readonly IWorkspaceContextSeeder _seeder;

    public LaunchSkillCommandHandler(ITerminalLauncher launcher, IWorkspaceContextSeeder seeder)
    {
        _launcher = launcher;
        _seeder = seeder;
    }

    public async Task<bool> Handle(LaunchSkillCommand request, CancellationToken cancellationToken)
    {
        await _seeder.SeedAsync(request.WorkspacePath, cancellationToken);
        return _launcher.Launch(request.WorkspacePath, request.RenderedCommand, request.Snap, request.ModelId);
    }
}
