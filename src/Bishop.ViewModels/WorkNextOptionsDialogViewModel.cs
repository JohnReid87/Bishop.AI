using Bishop.App.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed partial class WorkNextOptionsDialogViewModel : ObservableObject
{
    public const string AnyTagSentinel = "Any";
    public const string DefaultModelId = SkillModelOptions.DefaultModelId;

    public static readonly ModelOption[] Models =
    [
        new("claude-opus-4-7",           "Opus 4.7"),
        new("claude-sonnet-4-6",         "Sonnet 4.6"),
        new("claude-haiku-4-5-20251001", "Haiku 4.5"),
    ];

    public ModelOption[] AvailableModels => Models;

    public ObservableCollection<string> Tags { get; } = [];

    [ObservableProperty]
    public partial string SelectedTag { get; set; } = AnyTagSentinel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string MaxText { get; set; } = "10";

    [ObservableProperty]
    public partial ModelOption SelectedModel { get; set; } = null!;

    public bool CanConfirm => int.TryParse(MaxText, out var n) && n >= 0;

    public WorkNextOptionsDialogViewModel(IEnumerable<string> workspaceTagNames, string lastModelId = DefaultModelId)
    {
        Tags.Add(AnyTagSentinel);
        foreach (var name in workspaceTagNames)
            Tags.Add(name);
        SelectedModel = Models.FirstOrDefault(m => m.Id == lastModelId) ?? Models[1];
    }

    public WorkNextOptionsDialogViewModel() : this([]) { }

    public string? SelectedTagOrNull => SelectedTag == AnyTagSentinel ? null : SelectedTag;

    public int MaxValue => int.TryParse(MaxText, out var n) && n >= 0 ? n : 0;

    public string SelectedModelId => SelectedModel?.Id ?? DefaultModelId;
}
