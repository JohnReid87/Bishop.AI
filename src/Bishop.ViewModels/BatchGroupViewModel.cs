using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public readonly record struct BatchStats(string Name, int TotalCount, int DoneCount);

public sealed partial class BatchGroupViewModel : ObservableObject
{
    public Guid BatchId { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    public partial string BatchName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressDisplay))]
    public partial int DoneCount { get; set; }

    public string ProgressDisplay => $"({DoneCount}/{TotalCount})";

    public ObservableCollection<CardViewModel> Cards { get; } = [];
}
