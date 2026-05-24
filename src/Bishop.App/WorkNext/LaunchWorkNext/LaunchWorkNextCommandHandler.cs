using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.WorkNext.LaunchWorkNext;

public sealed class LaunchWorkNextCommandHandler : IRequestHandler<LaunchWorkNextCommand, bool>
{
    private readonly ITerminalLauncher _launcher;

    public LaunchWorkNextCommandHandler(ITerminalLauncher launcher) => _launcher = launcher;

    public Task<bool> Handle(LaunchWorkNextCommand request, CancellationToken cancellationToken)
    {
        var args = BuildArgs(request.Tag, request.Max, request.Model);
        return Task.FromResult(_launcher.LaunchCommand(request.WorkspacePath, "bishop", args, request.Snap));
    }

    internal static string[] BuildArgs(string? tag, int max, string? model = null)
    {
        var parts = new List<string> { "work-next" };
        if (!string.IsNullOrEmpty(tag))
        {
            parts.Add("--tag");
            parts.Add(tag);
        }
        parts.Add("--max");
        parts.Add(max.ToString());
        if (!string.IsNullOrEmpty(model))
        {
            parts.Add("--model");
            parts.Add(model);
        }
        return [.. parts];
    }
}
