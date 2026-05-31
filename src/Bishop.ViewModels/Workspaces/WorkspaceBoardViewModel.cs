using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Tags.ListTags;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class WorkspaceBoardViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly BoardSkillsCoordinator _skills;
    private readonly BoardGitCoordinator _git;
    private Guid _workspaceId;

    public bool IsCardSkillsButtonVisible
    {
        get => _skills.IsCardSkillsButtonVisible;
        set => _skills.IsCardSkillsButtonVisible = value;
    }

    public string WorkspacePath { get; set; } = string.Empty;

    public SkillMenuItem[] CardSkills => _skills.CardSkills;
    public SkillMenuItem[] WorkspaceSkills => _skills.WorkspaceSkills;

    public ObservableCollection<LaneViewModel> Lanes { get; } = [];

    public BatchStagingTrayViewModel StagingTray { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public bool IsSearchEmpty => string.IsNullOrEmpty(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        var batchStats = BoardBatchStats.Compute(Lanes);
        foreach (var lane in Lanes)
        {
            lane.ApplyFilter(value);
            lane.RebuildLaneItems(batchStats);
        }
        OnPropertyChanged(nameof(IsSearchEmpty));
    }

    public void RefreshLaneItems()
    {
        var batchStats = BoardBatchStats.Compute(Lanes);
        foreach (var lane in Lanes)
            lane.RebuildLaneItems(batchStats);
    }

    public IEnumerable<CardViewModel> SelectedCards =>
        Lanes.SelectMany(l => l.Cards).Where(c => c.IsSelected);

    public int SelectionCount => SelectedCards.Count();

    public bool HasSelection => SelectionCount > 0;

    public string SelectionLabel => SelectionCount == 1 ? "1 selected" : $"{SelectionCount} selected";

    public void ToggleCardSelection(CardViewModel card)
    {
        BoardSelection.Toggle(card, StagingTray);
        RaiseSelectionPropertyChanges();
    }

    public void ClearSelection()
    {
        BoardSelection.Clear(Lanes, StagingTray);
        RaiseSelectionPropertyChanges();
    }

    private void RaiseSelectionPropertyChanges()
    {
        OnPropertyChanged(nameof(SelectedCards));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
    }

    public WorkspaceBoardViewModel(ISender mediator, IAppSettings appSettings)
    {
        _mediator = mediator;
        _skills = new BoardSkillsCoordinator(mediator, appSettings, () => WorkspacePath);
        _git = new BoardGitCoordinator(mediator);
    }

    public async Task LoadAsync(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        await RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        var ctx = new BoardRefresher.Context(
            _mediator,
            _workspaceId,
            Lanes,
            CreateLaneVm,
            IsCardSkillsButtonVisible,
            SearchText);
        return BoardRefresher.RefreshAsync(ctx);
    }

    private LaneViewModel CreateLaneVm(string name) => new(_mediator, () => RefreshCommand.ExecuteAsync(null))
    {
        WorkspaceId = _workspaceId,
        Name = name,
    };

    // ── Skills ───────────────────────────────────────────────────────────────

    public async Task LoadSkillsAsync() => await _skills.LoadAsync();

    public Task<IReadOnlyList<SkillLaunchItem>> BuildWorkspaceSkillLaunchItemsAsync()
        => _skills.BuildWorkspaceLaunchItemsAsync();

    public Task<IReadOnlyList<SkillLaunchItem>> BuildCardSkillLaunchItemsAsync(CardViewModel card)
        => _skills.BuildCardLaunchItemsAsync(card);

    public Task LaunchAsync(SkillLaunchItem item, string? stagedText, TerminalSnap snap, string modelId)
        => _skills.LaunchAsync(item, stagedText, snap, modelId);

    public Task LaunchWorkspaceSkillByNameAsync(string skillName, string modelId, TerminalSnap snap)
        => _skills.LaunchWorkspaceByNameAsync(skillName, modelId, snap);

    public Task<SkillLaunchItem?> BuildWorkspaceSkillLaunchItemAsync(string skillName)
        => _skills.BuildWorkspaceLaunchItemByNameAsync(skillName);

    public Task SetSkillModelAsync(string skillName, string modelId)
        => _skills.SetSkillModelAsync(skillName, modelId);

    // ── Workspace launch ─────────────────────────────────────────────────────

    public async Task<bool> LaunchClaudeAsync(string workspacePath, TerminalSnap snap)
        => await _mediator.Send(new LaunchWorkspaceCommand(workspacePath, snap));

    public async Task<bool> LaunchTerminalAsync(string workspacePath, TerminalSnap snap)
        => await _mediator.Send(new LaunchPlainTerminalCommand(workspacePath, snap));

    // ── Commits ──────────────────────────────────────────────────────────────

    public Task<RecentCommitsResult> GetRecentCommitsAsync(string workspacePath)
        => _git.GetRecentCommitsAsync(workspacePath);

    public Task<string> GetCurrentBranchAsync(string workspacePath)
        => _git.GetCurrentBranchAsync(workspacePath);

    public Task<PushOutcome> PushAsync(string workspacePath, bool setUpstream = false)
        => _git.PushAsync(workspacePath, setUpstream);

    // ── Card operations ──────────────────────────────────────────────────────

    public async Task ToggleCardClosedAsync(Guid cardId, bool isClosed)
    {
        if (isClosed)
            await _mediator.Send(new ReopenCardCommand(cardId));
        else
            await _mediator.Send(new CloseCardCommand(cardId));
    }

    public async Task<IReadOnlyList<TagInfo>> GetTagsAsync()
        => await _mediator.Send(new ListTagsQuery());

    public async Task UpdateCardTagAsync(Guid cardId, string tagName)
        => await _mediator.Send(new UpdateCardCommand(cardId, null, null, true, tagName));

    public async Task MoveCardAsync(Guid cardId, string targetLane, int position)
        => await _mediator.Send(new MoveCardCommand(cardId, targetLane, position));

    // ── GitHub settings ──────────────────────────────────────────────────────

    public async Task SetGitHubRepoAsync(Guid workspaceId, string repo)
        => await _mediator.Send(new SetWorkspaceGitHubRepoCommand(workspaceId, repo));

    public async Task UnsetGitHubRepoAsync(Guid workspaceId)
        => await _mediator.Send(new UnsetWorkspaceGitHubRepoCommand(workspaceId));
}
