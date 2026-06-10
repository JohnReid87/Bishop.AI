using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;

namespace Bishop.UI.Views.Findings;

public sealed partial class FindingsPage : Page
{
    private readonly ISafeAsyncRunner _safeAsync;

    public FindingsViewModel ViewModel { get; }

    private FindingsPageNavArgs? _navArgs;

    public FindingsPage()
    {
        ViewModel = App.Services.GetRequiredService<FindingsViewModel>();
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is FindingsPageNavArgs args)
        {
            _navArgs = args;
            _ = _safeAsync.RunAsync(() => ViewModel.LoadAsync(
                args.WorkspaceId,
                args.WorkspacePath,
                args.SkillName,
                args.ProjectName));
        }
    }

    private void SortBySeverity_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleSort("severity");
    private void SortByTitle_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleSort("title");
    private void SortByLocation_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleSort("location");

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_navArgs?.Workspace is { } workspace)
        {
            Frame.Navigate(
                typeof(Bishop.UI.Views.Workspaces.WorkspaceDetailPage),
                new WorkspaceDetailPageNavArgs(workspace, _navArgs.SourceTab));
        }
        else if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void ConvertToCard_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not FindingItemViewModel item) return;
            await item.ConvertToCardCommand.ExecuteAsync(XamlRoot);
        });

    private async void OpenLinkedCard_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not FindingItemViewModel item) return;
            await item.OpenLinkedCardCommand.ExecuteAsync(XamlRoot);
        });

    private async void Dismiss_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (sender is not Button button) return;
            if (button.DataContext is not FindingItemViewModel item) return;

            var draft = item.RebuttalDraft;
            if (string.IsNullOrWhiteSpace(draft)) return;

            await item.DismissCommand.ExecuteAsync(draft);
            item.RebuttalDraft = string.Empty;

            if (button.Tag is FlyoutBase flyout) flyout.Hide();
        });
}
