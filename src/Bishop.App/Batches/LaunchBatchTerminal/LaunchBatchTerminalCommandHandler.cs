using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Batches.LaunchBatchTerminal;

internal sealed class LaunchBatchTerminalCommandHandler : IRequestHandler<LaunchBatchTerminalCommand, bool>
{
    private readonly ITerminalLauncher _launcher;

    public LaunchBatchTerminalCommandHandler(ITerminalLauncher launcher) => _launcher = launcher;

    public Task<bool> Handle(LaunchBatchTerminalCommand request, CancellationToken cancellationToken)
    {
        var args = request.Resume
            ? new[] { "batch", "run", request.BatchName, "--resume", "--model", request.Model }
            : new[] { "batch", "run", request.BatchName, "--model", request.Model };

        return Task.FromResult(_launcher.LaunchCommand(request.WorkspacePath, "bishop", args, request.Snap));
    }
}
