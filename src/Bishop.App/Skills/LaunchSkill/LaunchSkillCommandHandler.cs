using Bishop.App.Terminal;
using MediatR;

namespace Bishop.App.Skills.LaunchSkill;

public sealed class LaunchSkillCommandHandler : IRequestHandler<LaunchSkillCommand, bool>
{
    private readonly ITerminalLauncher _launcher;

    public LaunchSkillCommandHandler(ITerminalLauncher launcher) => _launcher = launcher;

    public Task<bool> Handle(LaunchSkillCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(_launcher.Launch(request.WorkspacePath, request.RenderedCommand, request.Snap));
}
