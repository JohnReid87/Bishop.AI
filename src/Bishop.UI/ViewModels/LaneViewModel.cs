using Bishop.App.Cards.AddCard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class LaneViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly Func<Task> _refreshBoard;

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    [ObservableProperty]
    public partial bool IsDropTarget { get; set; }

    [ObservableProperty]
    public partial bool IsAddingCard { get; set; }

    [ObservableProperty]
    public partial string NewCardTitle { get; set; } = string.Empty;

    public LaneViewModel(IMediator mediator, Func<Task> refreshBoard)
    {
        _mediator = mediator;
        _refreshBoard = refreshBoard;
    }

    [RelayCommand]
    private void BeginAddCard() => IsAddingCard = true;

    [RelayCommand]
    private async Task ConfirmAddCardAsync(CancellationToken cancellationToken)
    {
        var title = NewCardTitle.Trim();
        if (string.IsNullOrEmpty(title)) return;
        await _mediator.Send(new AddCardCommand(Id, title), cancellationToken);
        NewCardTitle = string.Empty;
        IsAddingCard = false;
        await _refreshBoard();
    }

    [RelayCommand]
    private void CancelAddCard()
    {
        IsAddingCard = false;
        NewCardTitle = string.Empty;
    }
}
