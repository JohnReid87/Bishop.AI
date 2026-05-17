using CommunityToolkit.Mvvm.ComponentModel;

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
}
