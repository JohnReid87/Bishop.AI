namespace Bishop.Core.Skills;

public sealed record InstalledSkill(
    string Name,
    string Description,
    IReadOnlyList<string> Scope,
    string? Command,
    bool Stage = false,
    string? StagePrompt = null,
    string? StagePrefill = null,
    string MarkdownBody = "",
    string SourcePath = "",
    SkillCategory Category = SkillCategory.Other,
    bool StageProjects = false,
    bool StageFilePicker = false);
