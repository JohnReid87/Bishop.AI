using Bishop.Core;
using Bishop.ViewModels.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Batches;

public sealed partial class BatchItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isNameEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    private bool _isStripExpanded = true;

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    public bool IsStripEmpty => Cards.Count == 0;
    public string ChevronGlyph => IsStripExpanded ? "" : "";
    public double StripOpacity => Status == BatchStatus.Closed ? 0.55 : 1.0;

    [RelayCommand]
    private void ToggleStrip() => IsStripExpanded = !IsStripExpanded;

    public string BranchName { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public BatchStatus Status { get; init; }
    public int CardCount { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public DateTimeOffset? StoppedAt { get; init; }
    public string StatusLabel => Status switch
    {
        BatchStatus.Working when IsMerged => "Merged",
        BatchStatus.Working when FinishedAt is not null => "Ready",
        BatchStatus.Working => "Working",
        BatchStatus.Closed => "Closed",
        _ => "Open"
    };
    public string CardCountLabel => CardCount == 1 ? "1 card" : $"{CardCount} cards";

    public bool IsMerged { get; init; }
    public bool BranchExists { get; init; }
    public bool WorktreeExists { get; init; }

    private const string ActiveColor = "#7fa87a";
    private const string InactiveColor = "#404040";
    public string IsMergedColor => IsMerged ? ActiveColor : InactiveColor;
    public string BranchExistsColor => BranchExists ? ActiveColor : InactiveColor;
    public string WorktreeExistsColor => WorktreeExists ? ActiveColor : InactiveColor;

    private BatchItemActions Actions => BatchItemActions.For(Status, FinishedAt, StoppedAt, IsMerged, BranchExists, WorktreeExists);

    public bool CanRun => Actions.CanRun;
    public bool CanPause => Actions.CanPause;
    public bool CanResume => Actions.CanResume;
    public bool CanMerge => Actions.CanMerge;
    public bool CanCleanUp => Actions.CanCleanUp;
    public bool CanAbandon => Actions.CanAbandon;
    public bool CanRemove => Actions.CanRemove;
}
