namespace Bishop.App.Skills;

public static class ClaudeModels
{
    public const string Fable5 = "claude-fable-5";
    public const string Opus48 = "claude-opus-4-8";
    public const string Opus47 = "claude-opus-4-7";
    public const string Sonnet46 = "claude-sonnet-4-6";
    public const string Haiku45 = "claude-haiku-4-5-20251001";

    public const string Fable5Display = "Fable 5";
    public const string Opus48Display = "Opus 4.8";
    public const string Opus47Display = "Opus 4.7";
    public const string Sonnet46Display = "Sonnet 4.6";
    public const string Haiku45Display = "Haiku 4.5";

    /// <summary>Friendly display name for a model ID, or the raw ID when unrecognised.</summary>
    public static string DisplayFor(string modelId) => modelId switch
    {
        Fable5 => Fable5Display,
        Opus48 => Opus48Display,
        Opus47 => Opus47Display,
        Sonnet46 => Sonnet46Display,
        Haiku45 => Haiku45Display,
        _ => modelId,
    };
}
