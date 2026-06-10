using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Pty.Net;

namespace Bishop.Life.App;

/// <summary>
/// Minimal Windows Terminal + <c>claude</c> launcher for Bishop.Life.App.
/// Mirrors the <c>wt.exe</c> + <c>cmd /k claude</c> pattern from
/// <c>Bishop.App.Services.Terminal.TerminalLauncher</c>; duplicated here
/// rather than referenced so Bishop.Life.App stays free of a Bishop.App
/// dependency (see card #1007 / DIRECTION.md §3).
/// Also exposes <see cref="TryLaunchClaudePty"/> for the embedded stand-up
/// experience (card #1053) — when the ConPTY spawn fails the host falls
/// through to the wt.exe path so the user still gets a working stand-up.
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
    /// Attempts to spawn <c>claude &lt;claudeArgs&gt;</c> via ConPTY at the
    /// requested terminal size. Returns <c>null</c> when Pty.Net fails (missing
    /// ConPTY support, claude not on PATH, or any native error) — the caller
    /// should fall through to <see cref="LaunchClaude"/>.
    /// </summary>
    public ClaudePtySession? TryLaunchClaudePty(string workingDirectory, string claudeArgs, int cols, int rows)
    {
        if (cols < 1) cols = 80;
        if (rows < 1) rows = 30;
        try
        {
            // cmd.exe /c is the same trampoline the wt.exe path uses — Pty.Net's
            // CreateProcess can't execute claude.cmd directly. /c (not /k) so the
            // PTY exits when claude exits, which is the cue to hide the terminal.
            var command = $"cmd.exe /c claude {claudeArgs}";
            var pty = PtyProvider.Spawn(command, cols, rows, workingDirectory, BackendOptions.ConPty);
            return new ClaudePtySession(pty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LifeTerminalLauncher: ConPTY spawn failed, falling back to wt.exe: {ex.Message}");
            return null;
        }
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
