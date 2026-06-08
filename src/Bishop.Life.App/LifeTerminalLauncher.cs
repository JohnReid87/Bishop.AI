using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace Bishop.Life.App;

/// <summary>
/// Minimal Windows Terminal + <c>claude</c> launcher for Bishop.Life.App.
/// Mirrors the <c>wt.exe</c> + <c>cmd /k claude</c> pattern from
/// <c>Bishop.App.Services.Terminal.TerminalLauncher</c>; duplicated here
/// rather than referenced so Bishop.Life.App stays free of a Bishop.App
/// dependency (see card #1007 / DIRECTION.md §3).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class LifeTerminalLauncher
{
    private readonly Func<string, bool> _fileExists;
    private readonly Action<ProcessStartInfo> _startProcess;

    public LifeTerminalLauncher() : this(File.Exists, psi => Process.Start(psi)) { }

    internal LifeTerminalLauncher(Func<string, bool> fileExists, Action<ProcessStartInfo> startProcess)
    {
        _fileExists = fileExists;
        _startProcess = startProcess;
    }

    /// <summary>
    /// Launches Windows Terminal at <paramref name="workingDirectory"/> running
    /// <c>cmd /k claude &lt;claudeArgs&gt;</c>. Falls back to <c>powershell.exe</c>
    /// when <c>wt.exe</c> isn't on disk. Returns <c>true</c> when wt was used.
    /// </summary>
    public bool LaunchClaude(string workingDirectory, string claudeArgs)
    {
        var wt = FindWindowsTerminal();
        if (wt is not null)
        {
            var psi = new ProcessStartInfo { FileName = wt, UseShellExecute = false };
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(workingDirectory);
            // cmd.exe /k resolves claude.cmd; wt -- claude fails because wt uses
            // CreateProcess directly, which can't execute .cmd wrapper scripts.
            psi.ArgumentList.Add("cmd.exe");
            psi.ArgumentList.Add("/k");
            psi.ArgumentList.Add("claude");
            psi.ArgumentList.Add(claudeArgs);
            _startProcess(psi);
            return true;
        }

        var fallback = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        fallback.ArgumentList.Add("-NoExit");
        fallback.ArgumentList.Add("-Command");
        fallback.ArgumentList.Add("claude");
        fallback.ArgumentList.Add(claudeArgs);
        _startProcess(fallback);
        return false;
    }

    private string? FindWindowsTerminal()
    {
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        if (_fileExists(alias)) return alias;

        var invalid = Path.GetInvalidPathChars();
        foreach (var raw in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            var segment = raw.Trim();
            if (segment.Length == 0 || segment.IndexOfAny(invalid) >= 0) continue;
            var candidate = Path.Combine(segment, "wt.exe");
            if (_fileExists(candidate)) return candidate;
        }
        return null;
    }
}
