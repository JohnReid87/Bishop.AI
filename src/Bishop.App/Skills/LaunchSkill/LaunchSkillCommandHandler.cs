using MediatR;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Bishop.App.Skills.LaunchSkill;

[SupportedOSPlatform("windows")]
public sealed class LaunchSkillCommandHandler : IRequestHandler<LaunchSkillCommand, bool>
{
    public Task<bool> Handle(LaunchSkillCommand request, CancellationToken cancellationToken)
    {
        var fullPath = BuildFullPath();
        var wt = FindWindowsTerminal();

        if (wt is not null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = wt,
                Arguments = $"-d \"{request.WorkspacePath}\" cmd.exe /k claude \"{request.RenderedCommand}\"",
                UseShellExecute = false,
            };
            psi.Environment["PATH"] = fullPath;
            Process.Start(psi);
            return Task.FromResult(true);
        }

        var psFallback = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -Command claude \"{request.RenderedCommand}\"",
            WorkingDirectory = request.WorkspacePath,
            UseShellExecute = false,
        };
        psFallback.Environment["PATH"] = fullPath;
        Process.Start(psFallback);
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

    private static string BuildFullPath()
    {
        using var machineEnv = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
        using var userEnv = Registry.CurrentUser.OpenSubKey(@"Environment");

        var machine = Environment.ExpandEnvironmentVariables(
            machineEnv?.GetValue("Path", "") as string ?? "");
        var user = Environment.ExpandEnvironmentVariables(
            userEnv?.GetValue("Path", "") as string ?? "");

        return string.IsNullOrEmpty(user) ? machine : $"{machine};{user}";
    }
}
