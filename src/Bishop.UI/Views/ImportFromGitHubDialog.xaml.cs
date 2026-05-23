using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Services.GitHub;
using Bishop.ViewModels;
using MediatR;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;

namespace Bishop.UI.Views;

public sealed partial class ImportFromGitHubDialog : ContentDialog
{
    private readonly Guid _workspaceId;
    private readonly string _repo;
    private readonly IMediator _mediator;
    private readonly IGhCli _ghCli;

    public ImportFromGitHubDialogViewModel ViewModel { get; } = new();

    public ImportFromGitHubDialog(Guid workspaceId, string repo, IMediator mediator, IGhCli ghCli)
    {
        _workspaceId = workspaceId;
        _repo = repo;
        _mediator = mediator;
        _ghCli = ghCli;
        InitializeComponent();
        IsPrimaryButtonEnabled = false;
        Loaded += async (_, _) => await LoadLabelsAsync();
        PrimaryButtonClick += OnImportClick;
    }

    private async Task LoadLabelsAsync()
    {
        ViewModel.IsBusy = true;
        try
        {
            var json = await _ghCli.RunCaptureAsync(["label", "list", "--repo", _repo, "--json", "name", "--limit", "100"]);
            var labels = JsonSerializer.Deserialize<List<GhLabelDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            foreach (var label in labels.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
                ViewModel.Labels.Add(label.Name);
        }
        catch
        {
            // Label loading is best-effort; the import still works without a filter.
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsBusy = true;
        IsPrimaryButtonEnabled = false;
        try
        {
            var result = await _mediator.Send(new ImportFromGitHubCommand(_workspaceId, ViewModel.LabelFilter, ViewModel.Limit, DryRun: true));
            ViewModel.PreviewItems.Clear();
            foreach (var card in result.Imported)
                ViewModel.PreviewItems.Add($"#{card.GitHubIssueNumber} {card.Title}");
            ViewModel.ResultSummary = $"{result.Imported.Count} to import, {result.SkippedAlreadyPresent.Count} already present";
            ViewModel.HasResults = true;
            IsPrimaryButtonEnabled = result.Imported.Count > 0;
        }
        catch (Exception ex)
        {
            ViewModel.ResultSummary = $"Preview failed: {ex.Message}";
            ViewModel.HasResults = true;
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async void OnImportClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        args.Cancel = true;
        ViewModel.IsBusy = true;
        IsPrimaryButtonEnabled = false;
        try
        {
            var result = await _mediator.Send(new ImportFromGitHubCommand(_workspaceId, ViewModel.LabelFilter, ViewModel.Limit, DryRun: false));
            ViewModel.PreviewItems.Clear();
            var summary = $"{result.Imported.Count} imported, {result.SkippedAlreadyPresent.Count} skipped";
            if (result.Failed.Count > 0)
                summary += $", {result.Failed.Count} failed";
            ViewModel.ResultSummary = summary;
            ViewModel.HasResults = true;
            ViewModel.WasImported = result.Imported.Count > 0;
        }
        catch (Exception ex)
        {
            ViewModel.ResultSummary = $"Import failed: {ex.Message}";
            ViewModel.HasResults = true;
            IsPrimaryButtonEnabled = true;
        }
        finally
        {
            ViewModel.IsBusy = false;
            deferral.Complete();
        }
    }

    private sealed class GhLabelDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
