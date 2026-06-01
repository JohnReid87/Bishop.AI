using Bishop.ViewModels.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Batches;

public readonly record struct BatchStats(string Name, int TotalCount, int DoneCount, int AccentIndex);

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
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    public partial int DoneCount { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public string ProgressDisplay => $"({DoneCount}/{TotalCount})";

    public ObservableCollection<CardViewModel> Cards { get; } = [];
}
