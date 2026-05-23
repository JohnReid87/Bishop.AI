namespace Bishop.App.Services.Claude;

public interface IClaudeCliRunner
{
    Task<ClaudeRunResult> RunPromptAsync(
        string workspacePath,
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default);
}
