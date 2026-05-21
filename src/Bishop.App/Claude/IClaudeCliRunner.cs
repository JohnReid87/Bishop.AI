namespace Bishop.App.Claude;

public interface IClaudeCliRunner
{
    Task<ClaudeRunResult> RunPromptAsync(
        string workspacePath,
        string prompt,
        CancellationToken cancellationToken = default);
}
