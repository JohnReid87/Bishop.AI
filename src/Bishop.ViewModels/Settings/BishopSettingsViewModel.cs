using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.Core.Skills;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Settings;

public sealed partial class BishopSettingsViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;

    public string WorkspacePath { get; set; } = string.Empty;

    public ObservableCollection<SkillLaunchItem> MetaSkills { get; } = [];

    public BishopSettingsViewModel(ISender mediator, IAppSettings appSettings)
    {
        _mediator = mediator;
        _appSettings = appSettings;
    }

    public async Task LoadAsync()
    {
        var skills = await _mediator.Send(new DiscoverSkillsQuery());
        var path = ResolveWorkspacePath();
        MetaSkills.Clear();
        foreach (var skill in skills.Where(s => s.Category == SkillCategory.Meta))
            MetaSkills.Add(await BuildLaunchItemAsync(skill, path));
    }

    public async Task LaunchAsync(SkillLaunchItem item, string? stagedText, TerminalSnap snap, string modelId)
    {
        var command = string.IsNullOrWhiteSpace(stagedText)
            ? item.RenderedCommand
            : $"{item.RenderedCommand} {stagedText}";
        await _mediator.Send(new LaunchSkillCommand(ResolveWorkspacePath(), command, snap, modelId));
    }

    public async Task SetSkillModelAsync(string skillName, string modelId)
        => await _appSettings.SetAsync($"skill.{skillName}.last_model", modelId);

    private async Task<SkillLaunchItem> BuildLaunchItemAsync(InstalledSkill skill, string workspacePath)
    {
        var command = string.IsNullOrWhiteSpace(skill.Command)
            ? $"/{skill.Name}"
            : SkillCommandRenderer.Render(skill.Command, null, null, null, workspacePath);

        var savedModel = SkillModelOptions.ResolveModelId(
            await _appSettings.GetAsync($"skill.{skill.Name}.last_model"));

        var requiresStage = SkillStaging.ShouldShowStageDialog(skill, hasCard: false);
        var prefill = skill.StagePrefill is null
            ? null
            : SkillCommandRenderer.Render(skill.StagePrefill, null, null, null, workspacePath).Trim();

        return new SkillLaunchItem(
            Name: skill.Name,
            GroupHeader: null,
            SavedModelId: savedModel,
            RenderedCommand: command,
            RequiresStage: requiresStage,
            StagePrompt: skill.StagePrompt,
            StagePrefill: string.IsNullOrEmpty(prefill) ? null : prefill,
            MarkdownBody: skill.MarkdownBody);
    }

    private string ResolveWorkspacePath() =>
        string.IsNullOrWhiteSpace(WorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : WorkspacePath;
}
