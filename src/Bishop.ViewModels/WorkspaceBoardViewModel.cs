using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTagsByWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed partial class WorkspaceBoardViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private Guid _workspaceId;

    public bool IsCardSkillsButtonVisible { get; set; }

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
        var result = new Dictionary<Guid, BatchStats>();
        foreach (var lane in Lanes)
        {
            foreach (var card in lane.Cards)
            {
                if (card.BatchId is not { } batchId) continue;
                result.TryGetValue(batchId, out var existing);
                result[batchId] = new BatchStats(
                    existing.Name is not (null or "") ? existing.Name : card.BatchName ?? string.Empty,
                    existing.TotalCount + 1,
                    existing.DoneCount + (card.LaneName == Bishop.Core.SystemLaneNames.Done ? 1 : 0));
            }
        }
        return result;
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

    public WorkspaceBoardViewModel(ISender mediator) => _mediator = mediator;

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
            IsSkillsButtonVisible = IsCardSkillsButtonVisible,
        };
    }
}
