using Bishop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class ManageWorkspacesDialog : ContentDialog
{
    public WorkspaceManagerViewModel ViewModel { get; }

    public ManageWorkspacesDialog(WorkspaceManagerViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not WorkspaceManagerItemViewModel item) return;

        var confirm = new ContentDialog
        {
            Title = $"Remove \"{item.Name}\"?",
            Content = "This will remove the workspace from Bishop.AI. Your files will not be affected.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        confirm.Resources["ContentDialogBackground"] = Application.Current.Resources["AppSurfaceBrush"];

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.RemoveAsync(item.Id);
    }

    private async void PurgeButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not WorkspaceManagerItemViewModel item) return;

        var confirm = new ContentDialog
        {
            Title = $"Purge \"{item.Name}\"?",
            Content = "This will permanently delete the workspace and all its cards. This cannot be undone.",
            PrimaryButtonText = "Purge",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        confirm.Resources["ContentDialogBackground"] = Application.Current.Resources["AppSurfaceBrush"];

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.PurgeAsync(item.Id);
    }
}
