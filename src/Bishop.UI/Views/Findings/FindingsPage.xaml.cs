using Bishop.ViewModels.Findings;
using Bishop.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
}
