namespace Bishop.App.Terminal;

public interface ITerminalLauncher
{
    // Returns true when launched in Windows Terminal, false when PowerShell fallback was used.
    bool Launch(string workingDirectory, string? claudeArgs, TerminalSnap? snap);
}
