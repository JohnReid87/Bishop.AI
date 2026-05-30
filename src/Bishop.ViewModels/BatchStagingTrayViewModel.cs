using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Bishop.App.Skills;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bishop.ViewModels;

public sealed partial class BatchStagingTrayViewModel : ObservableObject
{
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Branch { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Model { get; set; } = SkillModelOptions.DefaultModelId;

    [ObservableProperty]
    public partial string BaseBranch { get; set; } = string.Empty;

    private bool _branchManuallyEdited;
    private bool _suppressBranchEditedFlag;

    public ModelOption[] Models => SkillModels.All;

    public bool IsVisible => Cards.Count > 0;
    public int Count => Cards.Count;
    public string CreateLabel => $"Create ({Count})";
    public bool CanCreate => Count > 0 && !string.IsNullOrWhiteSpace(Name);

    public BatchStagingTrayViewModel()
    {
        Cards.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CreateLabel));
            OnPropertyChanged(nameof(CanCreate));
        };
    }

    partial void OnNameChanged(string value)
    {
        if (!_branchManuallyEdited)
        {
            var slug = Slugify(value.Trim());
            _suppressBranchEditedFlag = true;
            Branch = slug.Length > 0 ? $"bishop/{slug}" : string.Empty;
            _suppressBranchEditedFlag = false;
        }
        OnPropertyChanged(nameof(CanCreate));
    }

    partial void OnBranchChanged(string value)
    {
        if (!_suppressBranchEditedFlag)
            _branchManuallyEdited = true;
    }

    public void Reset()
    {
        Cards.Clear();
        Name = string.Empty;
        _suppressBranchEditedFlag = true;
        Branch = string.Empty;
        _suppressBranchEditedFlag = false;
        Model = SkillModelOptions.DefaultModelId;
        BaseBranch = string.Empty;
        _branchManuallyEdited = false;
    }

    public static string Slugify(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(' ', '-'), "[^a-z0-9-]", "");
}
