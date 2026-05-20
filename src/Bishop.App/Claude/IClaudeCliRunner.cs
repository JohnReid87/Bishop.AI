namespace Bishop.App.Claude;

public interface IClaudeCliRunner
{
    Task<int> RunPromptAsync(
        string workspacePath,
        string prompt,
        CancellationToken cancellationToken = default);
}
