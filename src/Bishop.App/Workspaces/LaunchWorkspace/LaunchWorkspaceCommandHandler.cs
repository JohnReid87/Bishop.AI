using MediatR;
using System.Diagnostics;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed class LaunchWorkspaceCommandHandler : IRequestHandler<LaunchWorkspaceCommand>
{
    public Task Handle(LaunchWorkspaceCommand request, CancellationToken cancellationToken)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "claude",
            WorkingDirectory = request.Path,
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }
}
