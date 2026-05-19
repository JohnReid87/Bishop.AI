using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System.IO;

namespace Bishop.UI.ViewModels;

public sealed partial class WorkspaceItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstLetter))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Position { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathMissingVisibility))]
    public partial bool IsPathMissing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedVisibility))]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial string? GitHubRepo { get; set; }

    public string FirstLetter => Name.Length > 0 ? Name[0..1].ToUpperInvariant() : "?";

    public Visibility IsSelectedVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PathMissingVisibility =>
        IsPathMissing ? Visibility.Visible : Visibility.Collapsed;

    partial void OnPathChanged(string value) =>
        IsPathMissing = !Directory.Exists(value);
}
