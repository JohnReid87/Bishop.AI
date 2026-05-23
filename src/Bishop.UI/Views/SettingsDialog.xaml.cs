using Bishop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialogViewModel ViewModel { get; }

    public SettingsDialog()
    {
        ViewModel = new SettingsDialogViewModel();
        InitializeComponent();
    }
}
