namespace Bishop.Core.Skills;

public sealed record InstalledSkill(
    string Name,
    string Description,
    string? Scope,
    string? Command,
    bool Stage = false,
    string? StagePrompt = null);
