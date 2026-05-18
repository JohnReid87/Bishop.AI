namespace Bishop.Core.Skills;

public sealed record InstalledSkill(
    string Name,
    string Description,
    string? Scope,
    string? Command);
