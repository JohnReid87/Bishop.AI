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

        Lanes.Clear();
        foreach (var lane in lanes)
        {
            var laneVm = new LaneViewModel { Id = lane.Id, Name = lane.Name };
            foreach (var card in cardsByLane[lane.Id])
            {
                var firstTag = card.CardTags.FirstOrDefault()?.Tag;
                laneVm.Cards.Add(new CardViewModel
                {
                    Id = card.Id,
                    LaneId = card.LaneId,
                    Number = card.Number,
                    Title = card.Title,
                    Description = card.Description,
                    LaneName = lane.Name,
                    Tags = card.CardTags.Select(ct => new CardTagViewModel { Name = ct.Tag.Name, Colour = ct.Tag.Colour }).ToList(),
                    FirstTagName = firstTag?.Name,
                    FirstTagColour = firstTag?.Colour,
                    IsClosed = card.IsClosed,
                    GitHubIssueNumber = card.GitHubIssueNumber,
                    GitHubPushedAt = card.GitHubPushedAt,
                });
            }
            Lanes.Add(laneVm);
        }
    }
}
