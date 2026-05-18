using Bishop.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class AddLaneDialog : ContentDialog
{
    public AddLaneDialogViewModel ViewModel { get; } = new();

    public AddLaneDialog()
    {
        InitializeComponent();
        IsPrimaryButtonEnabled = false;
        ViewModel.PropertyChanged += (_, _) =>
            IsPrimaryButtonEnabled = ViewModel.CanConfirm;
    }
}
