using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class ImportFromGitHubDialogViewModel : ObservableObject
{
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
}
