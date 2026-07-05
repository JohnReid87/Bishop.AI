using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.ViewModels.Workspaces;

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
    public partial bool IsPathMissing { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsHidden { get; set; }

    public string FirstLetter => Name.Length > 0 ? Name[0..1].ToUpperInvariant() : "?";
}
