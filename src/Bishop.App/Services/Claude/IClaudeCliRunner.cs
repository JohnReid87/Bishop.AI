using Bishop.App.Skills;

namespace Bishop.App.Services.Claude;

public interface IClaudeCliRunner
{
    Task<ClaudeRunResult> RunPromptAsync(
        string workspacePath,
        string prompt,
        string model = SkillModelOptions.DefaultModelId,
        int? cardNumber = null,
        CancellationToken cancellationToken = default);
}
