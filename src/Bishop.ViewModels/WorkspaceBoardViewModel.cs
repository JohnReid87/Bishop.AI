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

    public ObservableCollection<LaneViewModel> Lanes { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public bool IsSearchEmpty => string.IsNullOrEmpty(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        foreach (var lane in Lanes)
            lane.ApplyFilter(value);
        OnPropertyChanged(nameof(IsSearchEmpty));
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
        var cards = await _mediator.Send(new ListCardsByWorkspaceQuery(_workspaceId));
        var tags = await _mediator.Send(new ListTagsByWorkspaceQuery(_workspaceId));
        var tagColourByName = tags.ToDictionary(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);
        var cardsByLane = cards.ToLookup(c => c.LaneName, StringComparer.OrdinalIgnoreCase);

        // When the lane structure is unchanged, update only cards that actually changed so
        // ListView scroll positions are preserved and unnecessary Replace notifications are
        // avoided. A full rebuild (Lanes.Clear) is only needed when lanes are added,
        // removed, renamed, or reordered.
        if (Lanes.Count == lanes.Count && Lanes.Select(l => l.Name).SequenceEqual(lanes.Select(l => l.Name), StringComparer.OrdinalIgnoreCase))
        {
            foreach (var laneVm in Lanes)
            {
                var fresh = cardsByLane[laneVm.Name].ToList();
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
            return;
        }

        Lanes.Clear();
        foreach (var lane in lanes)
        {
            var laneVm = new LaneViewModel(_mediator, () => RefreshCommand.ExecuteAsync(null)) { WorkspaceId = _workspaceId, Name = lane.Name };
            foreach (var card in cardsByLane[lane.Name])
                laneVm.Cards.Add(BuildCardViewModel(card, lane.Name, tagColourByName));
            Lanes.Add(laneVm);
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            foreach (var laneVm in Lanes)
                laneVm.ApplyFilter(SearchText);
        }
    }

    private static bool Matches(CardViewModel vm, Bishop.Core.Card card, IReadOnlyDictionary<string, string> tagColourByName)
    {
        if (vm.Id != card.Id
            || vm.Title != card.Title
            || vm.Description != card.Description
            || vm.IsClosed != card.IsClosed
            || vm.GitHubIssueNumber != card.GitHubIssueNumber
            || vm.GitHubPushedAt != card.GitHubPushedAt
            || vm.LastAutoRunFailedAt != card.LastAutoRunFailedAt)
            return false;

        var expectedColour = card.TagName is { } name && tagColourByName.TryGetValue(name, out var c) ? c : null;
        if (vm.TagName != card.TagName || vm.TagColour != expectedColour)
            return false;

        return true;
    }

    private static CardViewModel BuildCardViewModel(Bishop.Core.Card card, string laneName, IReadOnlyDictionary<string, string> tagColourByName)
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
        };
    }
}
