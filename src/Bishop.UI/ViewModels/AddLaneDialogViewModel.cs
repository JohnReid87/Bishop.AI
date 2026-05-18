using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.UI.ViewModels;

public sealed partial class AddLaneDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string Name { get; set; } = string.Empty;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name);
}
