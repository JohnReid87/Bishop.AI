using Bishop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class ManageWorkspacesControl : UserControl
{
    public WorkspaceManagerViewModel ViewModel { get; }

    public ManageWorkspacesControl()
    {
        ViewModel = App.Services.GetRequiredService<WorkspaceManagerViewModel>();
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not WorkspaceManagerItemViewModel item) return;
            if (!await ConfirmFlyoutAsync((FrameworkElement)sender, "Remove")) return;
            await ViewModel.RemoveAsync(item.Id);
        });

    private async void PurgeButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not WorkspaceManagerItemViewModel item) return;
            if (!await ConfirmFlyoutAsync((FrameworkElement)sender, "Purge")) return;
            await ViewModel.PurgeAsync(item.Id);
        });

    // WinUI 3 forbids a second ContentDialog while one is already open (e.g. SettingsDialog).
    // A Flyout has no such constraint and anchors visually to the triggering button.
    private static Task<bool> ConfirmFlyoutAsync(FrameworkElement anchor, string verb)
    {
        var tcs = new TaskCompletionSource<bool>();

        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(4, 0, 4, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Are you sure you want to {verb.ToLower()} this workspace?",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 240,
            FontSize = 13,
        });
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var confirmBtn = new Button { Content = verb };
        var cancelBtn = new Button { Content = "Cancel" };
        buttonRow.Children.Add(confirmBtn);
        buttonRow.Children.Add(cancelBtn);
        panel.Children.Add(buttonRow);

        var flyout = new Flyout { Content = panel };
        confirmBtn.Click += (_, _) => { flyout.Hide(); tcs.TrySetResult(true); };
        cancelBtn.Click += (_, _) => { flyout.Hide(); tcs.TrySetResult(false); };
        flyout.Closed += (_, _) => tcs.TrySetResult(false);
        flyout.ShowAt(anchor);

        return tcs.Task;
    }
}
