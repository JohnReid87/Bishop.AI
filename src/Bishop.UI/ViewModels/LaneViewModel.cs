using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class LaneViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    public Visibility EmptyPlaceholderVisibility =>
        Cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public LaneViewModel()
    {
        Cards.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyPlaceholderVisibility));
    }
}
