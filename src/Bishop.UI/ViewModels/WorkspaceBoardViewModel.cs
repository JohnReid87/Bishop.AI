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
            var laneVm = new LaneViewModel { Name = lane.Name };
            foreach (var card in cardsByLane[lane.Id])
                laneVm.Cards.Add(new CardViewModel
                {
                    Id = card.Id,
                    Title = card.Title,
                    Description = card.Description,
                    Tags = card.CardTags.Select(ct => ct.Tag.Name).ToList(),
                });
            Lanes.Add(laneVm);
        }
    }
}
