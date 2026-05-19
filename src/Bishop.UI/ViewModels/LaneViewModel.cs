using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class LaneViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    [ObservableProperty]
    public partial bool IsDropTarget { get; set; }
}
