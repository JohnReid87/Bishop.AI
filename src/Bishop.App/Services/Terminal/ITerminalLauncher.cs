namespace Bishop.App.Services.Terminal;

public interface ITerminalLauncher
{
    // Returns true when launched in Windows Terminal, false when PowerShell fallback was used.
    bool Launch(string workingDirectory, string? claudeArgs, TerminalSnap? snap, string? modelId = null);

    // Launches a plain shell (no claude). Prefers pwsh.exe, falls back to powershell.exe.
    // Returns true when launched in Windows Terminal, false when launched directly.
    bool LaunchPlain(string workingDirectory, TerminalSnap? snap);

    // Launches an arbitrary CLI command in Windows Terminal (or PowerShell as a fallback).
    // Returns true when launched in Windows Terminal.
    bool LaunchCommand(string workingDirectory, string command, string[] args, TerminalSnap? snap);
}
