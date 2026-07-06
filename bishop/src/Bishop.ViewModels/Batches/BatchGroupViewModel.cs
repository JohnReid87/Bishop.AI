using Bishop.Core;
using Bishop.ViewModels.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Batches;

public readonly record struct BatchStats(
    string Name,
    int TotalCount,
    int DoneCount,
    int AccentIndex,
    BatchStatus Status,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? MergedAt,
    DateTimeOffset? StoppedAt);

public sealed partial class BatchGroupViewModel : ObservableObject
{
    private static readonly string[] _palette = ["#00FF41", "#BF40FF", "#1A8CFF", "#FF1493", "#00FFFF", "#FF6B00"];

    public Guid BatchId { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccentColor))]
    public partial int AccentIndex { get; set; }

    public string AccentColor => _palette[AccentIndex % _palette.Length];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    public partial string BatchName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    public partial int DoneCount { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    public partial BatchStatus Status { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    public partial DateTimeOffset? FinishedAt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    public partial DateTimeOffset? MergedAt { get; set; }

    // Distinguishes a Running batch (Working, no StoppedAt) from a Stopped one (Working, StoppedAt set);
    // the display state stays Working for both, so the split drives which header controls appear, not the label.
    [ObservableProperty]
    public partial DateTimeOffset? StoppedAt { get; set; }

    public string ProgressDisplay => $"({DoneCount}/{TotalCount})";

    private bool AllCardsDone => TotalCount > 0 && DoneCount == TotalCount;

    public BatchDisplayState DisplayState => BatchDisplayStates.Derive(Status, FinishedAt, MergedAt, AllCardsDone);
    public string StatusLabel => DisplayState.ToString();

    private bool IsWorking => DisplayState == BatchDisplayState.Working;
    private bool IsStopped => IsWorking && StoppedAt is not null;

    // State-driven header controls (see card #1115). Each state shows exactly its agreed set:
    // Open → Start · Abandon; Running → Pause; Stopped → Resume · Salvage · Abandon;
    // Finished → Review · Merge · Abandon; Merged → Clean up; Closed → none.
    public bool CanStart => DisplayState == BatchDisplayState.Open;
    public bool CanPause => IsWorking && StoppedAt is null;
    public bool CanResume => IsStopped;
    public bool CanSalvage => IsStopped;
    public bool CanReview => DisplayState == BatchDisplayState.Finished;
    public bool CanMerge => DisplayState == BatchDisplayState.Finished;
    public bool CanCleanUp => DisplayState == BatchDisplayState.Merged;
    public bool CanAbandon => DisplayState is BatchDisplayState.Open or BatchDisplayState.Finished || IsStopped;
    public bool HasAnyControl => CanStart || CanPause || CanResume || CanSalvage || CanReview || CanMerge || CanCleanUp || CanAbandon;

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    // The action flags derive from Status / FinishedAt / MergedAt / StoppedAt / counts; keep x:Bind
    // visibilities in sync by re-raising them whenever any of those observable inputs change.
    partial void OnStatusChanged(BatchStatus value) => NotifyControlsChanged();
    partial void OnFinishedAtChanged(DateTimeOffset? value) => NotifyControlsChanged();
    partial void OnMergedAtChanged(DateTimeOffset? value) => NotifyControlsChanged();
    partial void OnStoppedAtChanged(DateTimeOffset? value) => NotifyControlsChanged();
    partial void OnTotalCountChanged(int value) => NotifyControlsChanged();
    partial void OnDoneCountChanged(int value) => NotifyControlsChanged();

    private void NotifyControlsChanged()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanSalvage));
        OnPropertyChanged(nameof(CanReview));
        OnPropertyChanged(nameof(CanMerge));
        OnPropertyChanged(nameof(CanCleanUp));
        OnPropertyChanged(nameof(CanAbandon));
        OnPropertyChanged(nameof(HasAnyControl));
    }
}
