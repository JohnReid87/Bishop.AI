namespace Bishop.ViewModels.Skills;

/// <summary>
/// A fully view-ready skill launch row. The ViewModel resolves all command
/// rendering and stage decisions (which require <c>Bishop.App</c> /
/// <c>InstalledSkill</c>) up front, so code-behind only has to deal with
/// primitives and UI types — never the application layer directly.
/// </summary>
public sealed record SkillLaunchItem(
    string Name,
    string? GroupHeader,
    string SavedModelId,
    string RenderedCommand,
    bool RequiresStage,
    string? StagePrompt,
    string? StagePrefill,
    string MarkdownBody,
    bool StageProjects = false);
