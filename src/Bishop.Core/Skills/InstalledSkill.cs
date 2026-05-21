namespace Bishop.Core.Skills;

public sealed record InstalledSkill(
    string Name,
    string Description,
    IReadOnlyList<string> Scope,
    string? Command,
    bool Stage = false,
    string? StagePrompt = null,
    string? StagePrefill = null);
