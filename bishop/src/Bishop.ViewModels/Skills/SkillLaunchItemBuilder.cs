using Bishop.App.Services.Settings;
using Bishop.App.Skills;
using Bishop.Core.Skills;

namespace Bishop.ViewModels.Skills;

internal static class SkillLaunchItemBuilder
{
    internal const string LastModelKeyPrefix = "skill.";
    internal const string LastModelKeySuffix = ".last_model";

    internal static string LastModelKey(string skillName) =>
        $"{LastModelKeyPrefix}{skillName}{LastModelKeySuffix}";

    internal static Task<SkillLaunchItem> BuildAsync(
        SkillMenuItem menuItem,
        int? cardNumber,
        string? cardTitle,
        string? cardDescription,
        string workspacePath,
        IAppSettings appSettings,
        Guid? batchId = null,
        CancellationToken cancellationToken = default) =>
        BuildAsync(menuItem.Skill, menuItem.Name, menuItem.GroupHeader,
            cardNumber, cardTitle, cardDescription, workspacePath, appSettings, batchId, cancellationToken);

    internal static async Task<SkillLaunchItem> BuildAsync(
        InstalledSkill skill,
        string name,
        string? groupHeader,
        int? cardNumber,
        string? cardTitle,
        string? cardDescription,
        string workspacePath,
        IAppSettings appSettings,
        Guid? batchId = null,
        CancellationToken cancellationToken = default)
    {
        var command = string.IsNullOrWhiteSpace(skill.Command)
            ? $"/{skill.Name}"
            : SkillCommandRenderer.Render(skill.Command, cardNumber, cardTitle, cardDescription, workspacePath);

        var savedModel = SkillModelOptions.ResolveModelId(
            await appSettings.GetAsync(LastModelKey(skill.Name), cancellationToken));

        var requiresStage = SkillStaging.ShouldShowStageDialog(skill, hasCard: cardNumber is not null);

        var prefill = skill.StagePrefill is null
            ? null
            : SkillCommandRenderer.Render(skill.StagePrefill, cardNumber, cardTitle, cardDescription, workspacePath).Trim();

        return new SkillLaunchItem(
            Name: name,
            GroupHeader: groupHeader,
            SavedModelId: savedModel,
            RenderedCommand: command,
            RequiresStage: requiresStage,
            StagePrompt: skill.StagePrompt,
            StagePrefill: string.IsNullOrEmpty(prefill) ? null : prefill,
            MarkdownBody: skill.MarkdownBody,
            StageProjects: skill.StageProjects,
            StageFilePicker: skill.StageFilePicker,
            BatchId: batchId);
    }
}
