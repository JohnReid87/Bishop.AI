using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.Core.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed partial class BishopSettingsViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;

    public string WorkspacePath { get; set; } = string.Empty;

    public ObservableCollection<InstalledSkill> MetaSkills { get; } = [];

    public BishopSettingsViewModel(ISender mediator, IAppSettings appSettings)
    {
        _mediator = mediator;
        _appSettings = appSettings;
    }

    public async Task LoadAsync()
    {
        var skills = await _mediator.Send(new DiscoverSkillsQuery());
        MetaSkills.Clear();
        foreach (var s in skills.Where(s => s.Category == SkillCategory.Meta))
            MetaSkills.Add(s);
    }

    public async Task LaunchAsync(InstalledSkill skill)
    {
        var command = string.IsNullOrWhiteSpace(skill.Command)
            ? $"/{skill.Name}"
            : skill.Command;
        var path = string.IsNullOrWhiteSpace(WorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : WorkspacePath;
        await _mediator.Send(new LaunchSkillCommand(path, command));
    }

    public async Task LaunchCommandAsync(string workspacePath, string command, TerminalSnap snap, string? modelId)
        => await _mediator.Send(new LaunchSkillCommand(workspacePath, command, snap, modelId));

    public async Task<string?> GetSkillModelAsync(string skillName)
        => SkillModelOptions.ResolveModelId(await _appSettings.GetAsync($"skill.{skillName}.last_model"));

    public async Task SetSkillModelAsync(string skillName, string modelId)
        => await _appSettings.SetAsync($"skill.{skillName}.last_model", modelId);
}
