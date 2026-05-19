namespace Bishop.App.Terminal;

public interface ITerminalLauncher
{
    // Returns true when launched in Windows Terminal, false when PowerShell fallback was used.
    bool Launch(string workingDirectory, string? claudeArgs, TerminalSnap? snap, string? modelId = null);

    // Launches a plain shell (no claude). Prefers pwsh.exe, falls back to powershell.exe.
    // Returns true when launched in Windows Terminal, false when launched directly.
    bool LaunchPlain(string workingDirectory, TerminalSnap? snap);
}
