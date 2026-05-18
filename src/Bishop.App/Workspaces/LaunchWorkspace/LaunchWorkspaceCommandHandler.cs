using MediatR;
using System.Diagnostics;

namespace Bishop.App.Workspaces.LaunchWorkspace;

public sealed class LaunchWorkspaceCommandHandler : IRequestHandler<LaunchWorkspaceCommand, bool>
{
    public Task<bool> Handle(LaunchWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var wt = FindWindowsTerminal();

        if (wt is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = wt,
                Arguments = $"-d \"{request.Path}\"",
                UseShellExecute = false,
            });
            return Task.FromResult(true);
        }

        // Windows Terminal not available — open PowerShell at the workspace directory.
        // claude is not auto-launched here because GUI-spawned processes inherit a
        // narrower PATH than a normally-opened shell, so the command may not resolve.
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = request.Path,
            UseShellExecute = true,
        });
        return Task.FromResult(false);
    }

    private static string? FindWindowsTerminal()
    {
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(alias)) return alias;

        foreach (var segment in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                var candidate = Path.Combine(segment.Trim(), "wt.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }

        return null;
    }
}
