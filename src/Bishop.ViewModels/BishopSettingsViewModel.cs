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

    public string WorkspacePath { get; set; } = string.Empty;

    public ObservableCollection<InstalledSkill> MetaSkills { get; } = [];

    public BishopSettingsViewModel(ISender mediator) => _mediator = mediator;

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
}
