using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.UI.ViewModels;

public sealed partial class WorkNextOptionsDialogViewModel : ObservableObject
{
    public const string AnyTagSentinel = "Any";

    public ObservableCollection<string> Tags { get; } = [];

    [ObservableProperty]
    public partial string SelectedTag { get; set; } = AnyTagSentinel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string MaxText { get; set; } = "10";

    public bool CanConfirm => int.TryParse(MaxText, out var n) && n >= 0;

    public WorkNextOptionsDialogViewModel(IEnumerable<string> workspaceTagNames)
    {
        Tags.Add(AnyTagSentinel);
        foreach (var name in workspaceTagNames)
            Tags.Add(name);
    }

    public WorkNextOptionsDialogViewModel() : this([]) { }

    public string? SelectedTagOrNull => SelectedTag == AnyTagSentinel ? null : SelectedTag;

    public int MaxValue => int.TryParse(MaxText, out var n) && n >= 0 ? n : 0;
}
