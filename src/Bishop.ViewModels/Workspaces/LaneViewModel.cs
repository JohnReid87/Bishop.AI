using Bishop.App.Cards.AddCard;
using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class LaneViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly Func<Task> _refreshBoard;

    private string _currentFilter = string.Empty;

    public Guid WorkspaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<CardViewModel> Cards { get; } = [];
    public ObservableCollection<CardViewModel> FilteredCards { get; } = [];
    public ObservableCollection<object> LaneItems { get; } = [];

    public string DisplayName => $"{Name} ({Cards.Count})";

    public bool IsToDoLane => Name == SystemLaneNames.ToDo;
    public bool IsBacklogLane => Name == SystemLaneNames.Backlog;
    public bool IsDoneLane => Name == SystemLaneNames.Done;

    [ObservableProperty]
    public partial bool HasGitHubRepo { get; set; }

    public bool IsImportVisible => IsBacklogLane && HasGitHubRepo;
    public bool IsPushToGitHubVisible => IsDoneLane && HasGitHubRepo;

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

    public LaneViewModel(ISender mediator, Func<Task> refreshBoard)
    {
        _mediator = mediator;
        _refreshBoard = refreshBoard;
        Cards.CollectionChanged += OnCardsChanged;
        FilteredCards.CollectionChanged += OnFilteredCardsChanged;
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
        LaneItemsBuilder.ReconcileItems(FilteredCards, wanted);
    }

    public void RebuildLaneItems(IReadOnlyDictionary<Guid, BatchStats> batchStats)
    {
        var existingGroups = LaneItems.OfType<BatchGroupViewModel>().ToDictionary(g => g.BatchId);
        var (activeGroups, groupCards, target) = LaneItemsBuilder.Build(FilteredCards, batchStats, existingGroups);

        // In-place update avoids the Reset notification that Clear() emits, which can cause
        // ItemsRepeater to snapshot an empty collection before Add() notifications arrive.
        foreach (var (batchId, group) in activeGroups)
            LaneItemsBuilder.ReconcileItems(group.Cards, groupCards[batchId]);

        LaneItemsBuilder.ReconcileItems(LaneItems, target);
    }

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayName));
        RebuildFilteredCards();
    }

    partial void OnHasGitHubRepoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsImportVisible));
        OnPropertyChanged(nameof(IsPushToGitHubVisible));
    }

    private void OnFilteredCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayName));
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
        if (string.IsNullOrEmpty(title))
        {
            AddCardErrorMessage = "Title cannot be blank.";
            return;
        }
        AddCardErrorMessage = null;
        try
        {
            await _mediator.Send(new AddCardCommand(WorkspaceId, Name, title), cancellationToken);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            NewCardTitle = string.Empty;
            IsAddingCard = false;
            return;
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
