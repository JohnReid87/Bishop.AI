using Bishop.App.Skills;

namespace Bishop.ViewModels;

public static class SkillModels
{
    public static readonly ModelOption[] All =
    [
        new(ClaudeModels.Opus47,   ClaudeModels.Opus47Display),
        new(ClaudeModels.Sonnet46, ClaudeModels.Sonnet46Display),
        new(ClaudeModels.Haiku45,  ClaudeModels.Haiku45Display),
    ];
}
