using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.ViewModels.Shared;

public sealed partial class AddWorkspaceDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPickExisting { get; set; } = true;

    public bool CanConfirm =>
        !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(FolderPath);
}
