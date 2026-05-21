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

    private string _currentFilter = string.Empty;

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public ObservableCollection<CardViewModel> Cards { get; } = [];
    public ObservableCollection<CardViewModel> FilteredCards { get; } = [];

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

    public void ApplyFilter(string searchText)
    {
        _currentFilter = searchText;
        RebuildFilteredCards();
    }

    private void RebuildFilteredCards()
    {
        var wanted = string.IsNullOrEmpty(_currentFilter)
            ? Cards.ToList()
            : Cards.Where(c => c.MatchesSearch(_currentFilter)).ToList();

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < FilteredCards.Count)
            {
                if (!ReferenceEquals(FilteredCards[i], wanted[i]))
                    FilteredCards[i] = wanted[i];
            }
            else
                FilteredCards.Add(wanted[i]);
        }
        while (FilteredCards.Count > wanted.Count)
            FilteredCards.RemoveAt(FilteredCards.Count - 1);
    }

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanWorkNext));
        OnPropertyChanged(nameof(WorkNextTooltip));
        RebuildFilteredCards();
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
