using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System.IO;

namespace Bishop.UI.ViewModels;

public sealed partial class WorkspaceItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Position { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathMissingVisibility))]
    public partial bool IsPathMissing { get; set; }

    public Visibility PathMissingVisibility =>
        IsPathMissing ? Visibility.Visible : Visibility.Collapsed;

    partial void OnPathChanged(string value) =>
        IsPathMissing = !Directory.Exists(value);
}
