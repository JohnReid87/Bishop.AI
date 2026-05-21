using Bishop.App.Cards.AddCard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Bishop.UI.ViewModels;

public sealed partial class LaneViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly Func<Task> _refreshBoard;

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    public bool IsToDoLane => IsSystem && Name == "To Do";
    public bool CanWorkNext => IsToDoLane && Cards.Count > 0;
    public string WorkNextTooltip => CanWorkNext ? "Ralph it" : "No cards in To Do";

    [ObservableProperty]
    public partial bool IsDropTarget { get; set; }

    [ObservableProperty]
    public partial bool IsAddingCard { get; set; }

    [ObservableProperty]
    public partial string NewCardTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddCardError))]
    public partial string? AddCardErrorMessage { get; set; }

    public bool HasAddCardError => AddCardErrorMessage is not null;

    public LaneViewModel(IMediator mediator, Func<Task> refreshBoard)
    {
        _mediator = mediator;
        _refreshBoard = refreshBoard;
        Cards.CollectionChanged += OnCardsChanged;
    }

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanWorkNext));
        OnPropertyChanged(nameof(WorkNextTooltip));
    }

    [RelayCommand]
    private void BeginAddCard()
    {
        AddCardErrorMessage = null;
        IsAddingCard = true;
    }

    [RelayCommand]
    private async Task ConfirmAddCardAsync(CancellationToken cancellationToken)
    {
        var title = NewCardTitle.Trim();
        if (string.IsNullOrEmpty(title)) return;
        AddCardErrorMessage = null;
        try
        {
            await _mediator.Send(new AddCardCommand(Id, title), cancellationToken);
        }
        catch (Exception ex)
        {
            AddCardErrorMessage = ex.Message;
            return;
        }
        NewCardTitle = string.Empty;
        IsAddingCard = false;
        await _refreshBoard();
    }

    [RelayCommand]
    private void CancelAddCard()
    {
        IsAddingCard = false;
        NewCardTitle = string.Empty;
        AddCardErrorMessage = null;
    }
}
