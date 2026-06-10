using Bishop.App.Skills;
using Bishop.ViewModels.Shared;

namespace Bishop.ViewModels.Skills;

public static class SkillModels
{
    public static readonly ModelOption[] All =
    [
        new(ClaudeModels.Opus48,   ClaudeModels.Opus48Display),
        new(ClaudeModels.Opus47,   ClaudeModels.Opus47Display),
        new(ClaudeModels.Sonnet46, ClaudeModels.Sonnet46Display),
        new(ClaudeModels.Haiku45,  ClaudeModels.Haiku45Display),
    ];
}
