namespace Bishop.App.Skills;

public static class SkillModelOptions
{
    public const string DefaultModelId = ClaudeModels.Sonnet46;

    public static string ResolveModelId(string? savedModelId) => savedModelId ?? DefaultModelId;
}
