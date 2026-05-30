using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Services.GitHub;
using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Bishop.ViewModels.GitHub;

public sealed partial class ImportFromGitHubDialogViewModel : ObservableObject
{
    private readonly Guid _workspaceId;
    private readonly string _repo;
    private readonly ISender _mediator;
    private readonly IGhCli _ghCli;

    public ObservableCollection<string> Labels { get; } = ["(any)"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    public bool IsIdle => !IsBusy;

    [ObservableProperty]
    public partial string SelectedLabel { get; set; } = "(any)";

    [ObservableProperty]
    public partial string LimitText { get; set; } = "100";

    public int Limit => int.TryParse(LimitText, out var v) && v > 0 ? v : 100;

    public string? LabelFilter => SelectedLabel == "(any)" ? null : SelectedLabel;

    [ObservableProperty]
    public partial string ResultSummary { get; set; } = string.Empty;

    public ObservableCollection<string> PreviewItems { get; } = [];

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    public partial bool WasImported { get; set; }

    public ImportFromGitHubDialogViewModel(Guid workspaceId, string repo, ISender mediator, IGhCli ghCli)
    {
        _workspaceId = workspaceId;
        _repo = repo;
        _mediator = mediator;
        _ghCli = ghCli;
    }

    public async Task LoadLabelsAsync()
    {
        IsBusy = true;
        try
        {
            var json = await _ghCli.RunCaptureAsync(["label", "list", "--repo", _repo, "--json", "name", "--limit", "100"]);
            var labels = JsonSerializer.Deserialize<List<GhLabelDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            foreach (var label in labels.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
                Labels.Add(label.Name);
        }
        catch
        {
            // Label loading is best-effort; the import still works without a filter.
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> PreviewAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _mediator.Send(new ImportFromGitHubCommand(_workspaceId, LabelFilter, Limit, DryRun: true));
            PreviewItems.Clear();
            foreach (var card in result.Imported)
                PreviewItems.Add($"#{card.GitHubIssueNumber} {card.Title}");
            ResultSummary = $"{result.Imported.Count} to import, {result.SkippedAlreadyPresent.Count} already present";
            HasResults = true;
            return result.Imported.Count > 0;
        }
        catch (Exception ex)
        {
            ResultSummary = $"Preview failed: {ex.Message}";
            HasResults = true;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ImportAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _mediator.Send(new ImportFromGitHubCommand(_workspaceId, LabelFilter, Limit, DryRun: false));
            PreviewItems.Clear();
            var summary = $"{result.Imported.Count} imported, {result.SkippedAlreadyPresent.Count} skipped";
            if (result.Failed.Count > 0)
                summary += $", {result.Failed.Count} failed";
            ResultSummary = summary;
            HasResults = true;
            WasImported = result.Imported.Count > 0;
            return true;
        }
        catch (Exception ex)
        {
            ResultSummary = $"Import failed: {ex.Message}";
            HasResults = true;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private sealed class GhLabelDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
