using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Skills;
using MediatR;

namespace Bishop.ViewModels.Workspaces;

internal sealed class BoardSkillsCoordinator
{
    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;
    private readonly Func<string> _getWorkspacePath;

    public SkillMenuItem[] CardSkills { get; private set; } = [];
    public SkillMenuItem[] WorkspaceSkills { get; private set; } = [];
    public bool IsCardSkillsButtonVisible { get; set; }

    public BoardSkillsCoordinator(ISender mediator, IAppSettings appSettings, Func<string> getWorkspacePath)
    {
        _mediator = mediator;
        _appSettings = appSettings;
        _getWorkspacePath = getWorkspacePath;
    }

    public async Task LoadAsync()
    {
        var skills = await _mediator.Send(new DiscoverSkillsQuery());
        CardSkills = SkillMenuBuilder.Build(skills, "card");
        WorkspaceSkills = SkillMenuBuilder.Build(skills, "workspace");
        IsCardSkillsButtonVisible = CardSkills.Length > 0;
    }

    public Task<IReadOnlyList<SkillLaunchItem>> BuildWorkspaceLaunchItemsAsync()
        => BuildLaunchItemsAsync(WorkspaceSkills, card: null);

    public Task<IReadOnlyList<SkillLaunchItem>> BuildCardLaunchItemsAsync(CardViewModel card)
        => BuildLaunchItemsAsync(CardSkills, card);

    public async Task<SkillLaunchItem?> BuildWorkspaceLaunchItemByNameAsync(string skillName)
    {
        var menuItem = WorkspaceSkills.FirstOrDefault(s =>
            string.Equals(s.Skill.Name, skillName, StringComparison.OrdinalIgnoreCase));
        return menuItem is null ? null : await BuildLaunchItemAsync(menuItem, card: null);
    }

    public async Task LaunchAsync(SkillLaunchItem item, string? stagedText, TerminalSnap snap, string modelId)
    {
        var command = string.IsNullOrWhiteSpace(stagedText)
            ? item.RenderedCommand
            : $"{item.RenderedCommand} {stagedText}";
        await _mediator.Send(new LaunchSkillCommand(_getWorkspacePath(), command, snap, modelId));
    }

    public async Task LaunchWorkspaceByNameAsync(string skillName, string modelId, TerminalSnap snap)
    {
        var item = await BuildWorkspaceLaunchItemByNameAsync(skillName);
        if (item is null) return;
        await LaunchAsync(item, stagedText: null, snap, modelId);
    }

    public Task SetSkillModelAsync(string skillName, string modelId)
        => _appSettings.SetAsync(SkillLaunchItemBuilder.LastModelKey(skillName), modelId);

    private async Task<IReadOnlyList<SkillLaunchItem>> BuildLaunchItemsAsync(SkillMenuItem[] source, CardViewModel? card)
    {
        var items = new List<SkillLaunchItem>(source.Length);
        foreach (var menuItem in source)
            items.Add(await BuildLaunchItemAsync(menuItem, card));
        return items;
    }

    private Task<SkillLaunchItem> BuildLaunchItemAsync(SkillMenuItem menuItem, CardViewModel? card)
    {
        var workspacePath = _getWorkspacePath();
        return SkillLaunchItemBuilder.BuildAsync(menuItem, card?.Number, card?.Title, card?.Description,
            workspacePath, _appSettings);
    }
}
