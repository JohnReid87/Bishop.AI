namespace Bishop.App.Skills;

public static class SkillModelOptions
{
    public const string DefaultModelId = "claude-sonnet-4-6";

    public static string ResolveModelId(string? savedModelId) => savedModelId ?? DefaultModelId;
}
