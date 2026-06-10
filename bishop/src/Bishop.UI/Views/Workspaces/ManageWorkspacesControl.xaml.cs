using Bishop.UI.Views.Controls;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class ManageWorkspacesControl : UserControl
{
    private readonly ISafeAsyncRunner _safeAsync;

    public WorkspaceManagerViewModel ViewModel { get; }

    public ManageWorkspacesControl()
    {
        ViewModel = App.Services.GetRequiredService<WorkspaceManagerViewModel>();
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        InitializeComponent();
        Loaded += (_, _) => _safeAsync.RunAsync(ViewModel.LoadAsync);
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not WorkspaceManagerItemViewModel item) return;
            if (!await ConfirmFlyout.ShowAsync((FrameworkElement)sender,
                    "Are you sure you want to remove this workspace?", "Remove")) return;
            await ViewModel.RemoveAsync(item.Id);
        });

    private async void PurgeButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not WorkspaceManagerItemViewModel item) return;
            if (!await ConfirmFlyout.ShowAsync((FrameworkElement)sender,
                    "Are you sure you want to purge this workspace?", "Purge")) return;
            await ViewModel.PurgeAsync(item.Id);
        });
}
