using Bishop.App.Skills;
using Bishop.ViewModels.Shared;

namespace Bishop.ViewModels.Skills;

public static class SkillModels
{
    public static readonly ModelOption[] All =
    [
        new(ClaudeModels.Fable5,   ClaudeModels.Fable5Display),
        new(ClaudeModels.Opus48,   ClaudeModels.Opus48Display),
        new(ClaudeModels.Opus47,   ClaudeModels.Opus47Display),
        new(ClaudeModels.Sonnet46, ClaudeModels.Sonnet46Display),
        new(ClaudeModels.Sonnet5,  ClaudeModels.Sonnet5Display),
        new(ClaudeModels.Haiku45,  ClaudeModels.Haiku45Display),
    ];
}
