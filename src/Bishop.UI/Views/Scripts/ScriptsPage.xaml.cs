using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Bishop.UI.Views.Scripts;

public sealed partial class ScriptsPage : Page
{
    private readonly ISafeAsyncRunner _safeAsync;

    public ScriptsPageViewModel ViewModel { get; }

    public ScriptsPage()
    {
        ViewModel = App.Services.GetRequiredService<ScriptsPageViewModel>();
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = _safeAsync.RunAsync(ViewModel.LoadScriptsAsync);
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if ((sender as FrameworkElement)?.DataContext is not ScriptItemViewModel item)
                return;
            await ViewModel.RunScriptAsync(item);
        });

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (sender is not FrameworkElement element || element.DataContext is not ScriptItemViewModel item)
                return;
            var confirmed = await ConfirmDeleteFlyoutAsync(element);
            if (confirmed)
                item.DeleteCommand.Execute(null);
        });

    private static Task<bool> ConfirmDeleteFlyoutAsync(FrameworkElement anchor)
    {
        var tcs = new TaskCompletionSource<bool>();

        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(4, 0, 4, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = "Permanently delete this script from disk?",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 240,
            FontSize = 13,
        });
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var confirmBtn = new Button { Content = "Delete" };
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
