using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class WorkspaceBoardViewModel : ObservableObject
{
    private readonly IMediator _mediator;
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

    public WorkspaceBoardViewModel(IMediator mediator) => _mediator = mediator;

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
        var cardsByLane = cards.ToLookup(c => c.LaneId);

        // When the lane structure is unchanged, update only cards that actually changed so
        // ListView scroll positions are preserved and unnecessary Replace notifications are
        // avoided. A full rebuild (Lanes.Clear) is only needed when lanes are added,
        // removed, renamed, or reordered.
        if (Lanes.Count == lanes.Count && Lanes.Select(l => l.Id).SequenceEqual(lanes.Select(l => l.Id)))
        {
            foreach (var laneVm in Lanes)
            {
                var fresh = cardsByLane[laneVm.Id].ToList();
                for (var i = 0; i < fresh.Count; i++)
                {
                    var card = fresh[i];
                    if (i < laneVm.Cards.Count && Matches(laneVm.Cards[i], card))
                        continue;
                    var cardVm = BuildCardViewModel(card, laneVm.Name);
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
            var laneVm = new LaneViewModel(_mediator, () => RefreshCommand.ExecuteAsync(null)) { Id = lane.Id, Name = lane.Name, IsSystem = lane.IsSystem };
            foreach (var card in cardsByLane[lane.Id])
                laneVm.Cards.Add(BuildCardViewModel(card, lane.Name));
            Lanes.Add(laneVm);
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            foreach (var laneVm in Lanes)
                laneVm.ApplyFilter(SearchText);
        }
    }

    private static bool Matches(CardViewModel vm, Bishop.Core.Card card)
    {
        if (vm.Id != card.Id
            || vm.Title != card.Title
            || vm.Description != card.Description
            || vm.IsClosed != card.IsClosed
            || vm.GitHubIssueNumber != card.GitHubIssueNumber
            || vm.GitHubPushedAt != card.GitHubPushedAt
            || vm.Tags.Count != card.CardTags.Count)
            return false;

        for (var i = 0; i < vm.Tags.Count; i++)
        {
            var ct = card.CardTags.ElementAt(i).Tag;
            if (vm.Tags[i].Name != ct.Name || vm.Tags[i].Colour != ct.Colour)
                return false;
        }

        return true;
    }

    private static CardViewModel BuildCardViewModel(Bishop.Core.Card card, string laneName)
    {
        var firstTag = card.CardTags.FirstOrDefault()?.Tag;
        return new CardViewModel
        {
            Id = card.Id,
            LaneId = card.LaneId,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = laneName,
            Tags = card.CardTags.Select(ct => new CardTagViewModel { Name = ct.Tag.Name, Colour = ct.Tag.Colour }).ToList(),
            FirstTagName = firstTag?.Name,
            FirstTagColour = firstTag?.Colour,
            IsClosed = card.IsClosed,
            GitHubIssueNumber = card.GitHubIssueNumber,
            GitHubPushedAt = card.GitHubPushedAt,
        };
    }
}
