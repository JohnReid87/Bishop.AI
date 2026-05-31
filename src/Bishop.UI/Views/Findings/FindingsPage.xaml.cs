using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Bishop.UI.Views.Findings;

public sealed partial class FindingsPage : Page
{
    public FindingsViewModel ViewModel { get; }

    private string _sortKey = "title";
    private bool _sortAsc = true;

    public FindingsPage()
    {
        ViewModel = App.Services.GetRequiredService<FindingsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Bishop.ViewModels.Findings.FindingsPageNavArgs args)
        {
            _ = SafeAsync.RunAsync(async () =>
            {
                await ViewModel.LoadAsync(
                    args.WorkspaceId,
                    args.WorkspacePath,
                    args.GitHubRepo,
                    args.SkillName,
                    args.ProjectName);
                ApplyView();
            });
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FindingsViewModel.FilterText))
            ApplyView();
    }

    private void ApplyView()
    {
        var filter = ViewModel.FilterText;
        IEnumerable<FindingItemViewModel> items = ViewModel.Findings;
        if (!string.IsNullOrWhiteSpace(filter))
            items = items.Where(ViewModel.Matches);
        items = _sortKey switch
        {
            "severity" => _sortAsc
                ? items.OrderBy(SeverityRank).ThenBy(i => i.Title)
                : items.OrderByDescending(SeverityRank).ThenBy(i => i.Title),
            "location" => _sortAsc
                ? items.OrderBy(i => i.Location, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(i => i.Location, StringComparer.OrdinalIgnoreCase),
            _ => _sortAsc
                ? items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(i => i.Title, StringComparer.OrdinalIgnoreCase),
        };
        FindingsView.Source = items.ToList();
    }

    private static int SeverityRank(FindingItemViewModel f) => (f.Severity ?? string.Empty).ToLowerInvariant() switch
    {
        "critical" or "high" => 0,
        "medium" or "med" => 1,
        "low" or "info" => 2,
        _ => 3,
    };

    private void SortBySeverity_Click(object sender, RoutedEventArgs e) => ToggleSort("severity");
    private void SortByTitle_Click(object sender, RoutedEventArgs e) => ToggleSort("title");
    private void SortByLocation_Click(object sender, RoutedEventArgs e) => ToggleSort("location");

    private void ToggleSort(string key)
    {
        if (_sortKey == key) _sortAsc = !_sortAsc;
        else { _sortKey = key; _sortAsc = true; }
        ApplyView();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private async void ConvertToCard_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not FindingItemViewModel item) return;
            await item.ConvertToCardCommand.ExecuteAsync(XamlRoot);
        });

    private async void OpenLinkedCard_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not FindingItemViewModel item) return;
            await item.OpenLinkedCardCommand.ExecuteAsync(XamlRoot);
        });

    private async void Dismiss_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (sender is not Button button || button.Tag is not FindingItemViewModel item) return;

            DependencyObject current = button;
            StackPanel? root = null;
            while (current is not null)
            {
                if (current is StackPanel sp) { root = sp; break; }
                current = VisualTreeHelper.GetParent(current);
            }
            if (root is null) return;

            var box = FindDescendantByName(root, "DismissRebuttalBox") as TextBox;
            var rebuttal = box?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rebuttal)) return;

            await item.DismissCommand.ExecuteAsync(rebuttal);

            if (box is not null) box.Text = string.Empty;

            // Hide the containing flyout by walking up to the popup root.
            var popupRoot = root.XamlRoot is null ? null : VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot);
            if (popupRoot is not null)
            {
                foreach (var p in popupRoot)
                    if (p.Child is FrameworkElement fe && IsAncestor(box, fe))
                        p.IsOpen = false;
            }
        });

    private static bool IsAncestor(DependencyObject? descendant, DependencyObject ancestor)
    {
        var current = descendant;
        while (current is not null)
        {
            if (current == ancestor) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static DependencyObject? FindDescendantByName(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe && fe.Name == name) return root;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindDescendantByName(child, name);
            if (found is not null) return found;
        }
        return null;
    }
}
