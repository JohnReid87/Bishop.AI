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
        var sanitized = string.IsNullOrWhiteSpace(stagedText) ? null : SkillCommandRenderer.Sanitize(stagedText);
        var command = string.IsNullOrWhiteSpace(sanitized)
            ? item.RenderedCommand
            : $"{item.RenderedCommand} {sanitized}";
        await _mediator.Send(new LaunchSkillCommand(ResolveWorkspacePath(), command, snap, modelId));
    }

    public async Task SetSkillModelAsync(string skillName, string modelId)
        => await _appSettings.SetAsync(SkillLaunchItemBuilder.LastModelKey(skillName), modelId);

    private Task<SkillLaunchItem> BuildLaunchItemAsync(InstalledSkill skill, string workspacePath) =>
        SkillLaunchItemBuilder.BuildAsync(skill, skill.Name, groupHeader: null,
            cardNumber: null, cardTitle: null, cardDescription: null,
            workspacePath, _appSettings);

    private string ResolveWorkspacePath() =>
        string.IsNullOrWhiteSpace(WorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : WorkspacePath;
}
