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

    public void RebuildLaneItems(IReadOnlyDictionary<Guid, BatchStats> batchStats)
    {
        var existingGroups = LaneItems.OfType<BatchGroupViewModel>().ToDictionary(g => g.BatchId);
        var activeGroups = new Dictionary<Guid, BatchGroupViewModel>();
        var groupCards = new Dictionary<Guid, List<CardViewModel>>();
        var target = new List<object>(FilteredCards.Count);

        foreach (var card in FilteredCards)
        {
            if (card.BatchId is { } batchId)
            {
                if (!activeGroups.TryGetValue(batchId, out var group))
                {
                    if (!existingGroups.TryGetValue(batchId, out group))
                        group = new BatchGroupViewModel { BatchId = batchId };

                    if (batchStats.TryGetValue(batchId, out var s))
                        (group.BatchName, group.TotalCount, group.DoneCount, group.AccentIndex) = (s.Name, s.TotalCount, s.DoneCount, s.AccentIndex);
                    else
                        group.BatchName = card.BatchName ?? string.Empty;

                    activeGroups[batchId] = group;
                    groupCards[batchId] = [];
                    target.Add(group);
                }
                groupCards[batchId].Add(card);
            }
            else
                target.Add(card);
        }

        // In-place update avoids the Reset notification that Clear() emits, which can cause
        // ItemsRepeater to snapshot an empty collection before Add() notifications arrive.
        foreach (var (batchId, group) in activeGroups)
        {
            var wanted = groupCards[batchId];
            for (var i = 0; i < wanted.Count; i++)
            {
                if (i < group.Cards.Count)
                { if (!ReferenceEquals(group.Cards[i], wanted[i])) group.Cards[i] = wanted[i]; }
                else group.Cards.Add(wanted[i]);
            }
            while (group.Cards.Count > wanted.Count)
                group.Cards.RemoveAt(group.Cards.Count - 1);
        }

        for (var i = 0; i < target.Count; i++)
        {
            if (i < LaneItems.Count)
            { if (!ReferenceEquals(LaneItems[i], target[i])) LaneItems[i] = target[i]; }
            else LaneItems.Add(target[i]);
        }
        while (LaneItems.Count > target.Count)
            LaneItems.RemoveAt(LaneItems.Count - 1);
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
