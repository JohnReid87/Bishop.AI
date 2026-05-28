using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed partial class WorkspaceBoardViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;
    private Guid _workspaceId;

    public bool IsCardSkillsButtonVisible { get; set; }

    public SkillMenuItem[] CardSkills { get; private set; } = [];
    public SkillMenuItem[] WorkspaceSkills { get; private set; } = [];

    public ObservableCollection<LaneViewModel> Lanes { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public bool IsSearchEmpty => string.IsNullOrEmpty(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        var batchStats = ComputeBatchStats();
        foreach (var lane in Lanes)
        {
            lane.ApplyFilter(value);
            lane.RebuildLaneItems(batchStats);
        }
        OnPropertyChanged(nameof(IsSearchEmpty));
    }

    private IReadOnlyDictionary<Guid, BatchStats> ComputeBatchStats()
    {
        var raw = new Dictionary<Guid, (string Name, int TotalCount, int DoneCount, DateTimeOffset? CreatedAt)>();
        foreach (var lane in Lanes)
        {
            foreach (var card in lane.Cards)
            {
                if (card.BatchId is not { } batchId) continue;
                raw.TryGetValue(batchId, out var e);
                raw[batchId] = (
                    e.Name is not (null or "") ? e.Name : card.BatchName ?? string.Empty,
                    e.TotalCount + 1,
                    e.DoneCount + (card.LaneName == Bishop.Core.SystemLaneNames.Done ? 1 : 0),
                    e.CreatedAt ?? card.BatchCreatedAt);
            }
        }
        var indexByBatch = raw
            .OrderBy(kvp => kvp.Value.CreatedAt ?? DateTimeOffset.MaxValue)
            .Select((kvp, i) => (kvp.Key, Index: i))
            .ToDictionary(x => x.Key, x => x.Index);
        return raw.ToDictionary(
            kvp => kvp.Key,
            kvp => new BatchStats(kvp.Value.Name, kvp.Value.TotalCount, kvp.Value.DoneCount, indexByBatch[kvp.Key]));
    }

    public void RefreshLaneItems()
    {
        var batchStats = ComputeBatchStats();
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
        card.IsSelected = !card.IsSelected;
        OnPropertyChanged(nameof(SelectedCards));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
    }

    public void ClearSelection()
    {
        foreach (var lane in Lanes)
            foreach (var c in lane.Cards)
                c.IsSelected = false;
        OnPropertyChanged(nameof(SelectedCards));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
    }

    public WorkspaceBoardViewModel(ISender mediator, IAppSettings appSettings)
    {
        _mediator = mediator;
        _appSettings = appSettings;
    }

    public async Task LoadAsync(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var lanes = await _mediator.Send(new ListLanesByWorkspaceQuery(_workspaceId));
        var tags = await _mediator.Send(new ListTagsByWorkspaceQuery(_workspaceId));
        var tagColourByName = tags.ToDictionary(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);

        // When the lane structure is unchanged, update only cards that actually changed so
        // ListView scroll positions are preserved and unnecessary Replace notifications are
        // avoided. A full rebuild (Lanes.Clear) is only needed when lanes are added,
        // removed, renamed, or reordered.
        if (Lanes.Count == lanes.Count && Lanes.Select(l => l.Name).SequenceEqual(lanes.Select(l => l.Name), StringComparer.OrdinalIgnoreCase))
        {
            foreach (var laneVm in Lanes)
            {
                var fresh = (await _mediator.Send(new ListCardsByWorkspaceQuery(_workspaceId, LaneName: laneVm.Name))).ToList();
                for (var i = 0; i < fresh.Count; i++)
                {
                    var card = fresh[i];
                    if (i < laneVm.Cards.Count && Matches(laneVm.Cards[i], card, tagColourByName))
                        continue;
                    var cardVm = BuildCardViewModel(card, laneVm.Name, tagColourByName);
                    if (i < laneVm.Cards.Count)
                        laneVm.Cards[i] = cardVm;
                    else
                        laneVm.Cards.Add(cardVm);
                }
                while (laneVm.Cards.Count > fresh.Count)
                    laneVm.Cards.RemoveAt(laneVm.Cards.Count - 1);
            }
            var batchStats = ComputeBatchStats();
            foreach (var laneVm in Lanes)
                laneVm.RebuildLaneItems(batchStats);
            return;
        }

        Lanes.Clear();
        foreach (var lane in lanes)
        {
            var laneVm = new LaneViewModel(_mediator, () => RefreshCommand.ExecuteAsync(null)) { WorkspaceId = _workspaceId, Name = lane.Name };
            var cards = await _mediator.Send(new ListCardsByWorkspaceQuery(_workspaceId, LaneName: lane.Name));
            foreach (var card in cards)
                laneVm.Cards.Add(BuildCardViewModel(card, lane.Name, tagColourByName));
            Lanes.Add(laneVm);
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            foreach (var laneVm in Lanes)
                laneVm.ApplyFilter(SearchText);
        }

        var fullBatchStats = ComputeBatchStats();
        foreach (var laneVm in Lanes)
            laneVm.RebuildLaneItems(fullBatchStats);
    }

    private static bool Matches(CardViewModel vm, Bishop.Core.Card card, IReadOnlyDictionary<string, string> tagColourByName)
    {
        if (vm.Id != card.Id
            || vm.Title != card.Title
            || vm.Description != card.Description
            || vm.IsClosed != card.IsClosed
            || vm.GitHubIssueNumber != card.GitHubIssueNumber
            || vm.GitHubPushedAt != card.GitHubPushedAt
            || vm.LastAutoRunFailedAt != card.LastAutoRunFailedAt
            || vm.BatchId != card.BatchId)
            return false;

        var expectedColour = card.TagName is { } name && tagColourByName.TryGetValue(name, out var c) ? c : null;
        if (vm.TagName != card.TagName || vm.TagColour != expectedColour)
            return false;

        return true;
    }

    private CardViewModel BuildCardViewModel(Bishop.Core.Card card, string laneName, IReadOnlyDictionary<string, string> tagColourByName)
    {
        var tagColour = card.TagName is { } name && tagColourByName.TryGetValue(name, out var c) ? c : null;
        return new CardViewModel
        {
            Id = card.Id,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = laneName,
            TagName = card.TagName,
            TagColour = tagColour,
            IsClosed = card.IsClosed,
            GitHubIssueNumber = card.GitHubIssueNumber,
            GitHubPushedAt = card.GitHubPushedAt,
            LastAutoRunFailedAt = card.LastAutoRunFailedAt,
            BatchId = card.BatchId,
            BatchName = card.Batch?.Name,
            BatchCreatedAt = card.Batch?.CreatedAt,
            IsSkillsButtonVisible = IsCardSkillsButtonVisible,
        };
    }

    // ── Skills ───────────────────────────────────────────────────────────────

    public async Task LoadSkillsAsync()
    {
        var skills = await _mediator.Send(new DiscoverSkillsQuery());
        CardSkills = SkillMenuBuilder.Build(skills, "card");
        WorkspaceSkills = SkillMenuBuilder.Build(skills, "workspace");
        IsCardSkillsButtonVisible = CardSkills.Length > 0;
    }

    public async Task LaunchSkillAsync(string workspacePath, string rendered, TerminalSnap snap, string? modelId)
        => await _mediator.Send(new LaunchSkillCommand(workspacePath, rendered, snap, modelId));

    public async Task<string?> GetSkillModelAsync(string skillName)
        => SkillModelOptions.ResolveModelId(await _appSettings.GetAsync($"skill.{skillName}.last_model"));

    public async Task SetSkillModelAsync(string skillName, string modelId)
        => await _appSettings.SetAsync($"skill.{skillName}.last_model", modelId);

    // ── Workspace launch ─────────────────────────────────────────────────────

    public async Task<bool> LaunchClaudeAsync(string workspacePath, TerminalSnap snap)
        => await _mediator.Send(new LaunchWorkspaceCommand(workspacePath, snap));

    public async Task<bool> LaunchTerminalAsync(string workspacePath, TerminalSnap snap)
        => await _mediator.Send(new LaunchPlainTerminalCommand(workspacePath, snap));

    // ── Commits ──────────────────────────────────────────────────────────────

    public async Task<GetRecentCommitsResult> GetRecentCommitsAsync(string workspacePath)
        => await _mediator.Send(new GetRecentCommitsQuery(workspacePath));

    public async Task<PushResult> PushAsync(string workspacePath)
        => await _mediator.Send(new PushCommand(workspacePath));

    // ── Card operations ──────────────────────────────────────────────────────

    public async Task ToggleCardClosedAsync(Guid cardId, bool isClosed)
    {
        if (isClosed)
            await _mediator.Send(new ReopenCardCommand(cardId));
        else
            await _mediator.Send(new CloseCardCommand(cardId));
    }

    public async Task<IReadOnlyList<TagInfo>> GetTagsAsync()
        => await _mediator.Send(new ListTagsByWorkspaceQuery(_workspaceId));

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
