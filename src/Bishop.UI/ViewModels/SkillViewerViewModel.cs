using Bishop.App.Settings;
using Bishop.Core.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Text.Json;

namespace Bishop.UI.ViewModels;

public sealed partial class SkillViewerViewModel : ObservableObject
{
    internal const double DefaultPanelWidth = 480;
    internal const double MinPanelWidth = 280;
    internal const double MaxPanelWidth = 2000;

    private readonly IAppSettings _appSettings;
    private readonly string _prefsFilePath;
    private Guid _workspaceId;
    private bool _isLoadingPrefs;

    public SkillViewerViewModel(IAppSettings appSettings)
        : this(appSettings, DefaultPrefsFilePath()) { }

    internal SkillViewerViewModel(IAppSettings appSettings, string prefsFilePath)
    {
        _appSettings = appSettings;
        _prefsFilePath = prefsFilePath;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SkillName))]
    [NotifyPropertyChangedFor(nameof(ScopeText))]
    [NotifyPropertyChangedFor(nameof(CommandText))]
    [NotifyPropertyChangedFor(nameof(HasMetadata))]
    public partial InstalledSkill? Skill { get; set; }

    [ObservableProperty]
    public partial string MarkdownBody { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial double PanelWidth { get; set; } = DefaultPanelWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModelLabel))]
    public partial string ModelId { get; set; } = WorkNextOptionsDialogViewModel.DefaultModelId;

    public string SkillName => Skill?.Name ?? string.Empty;

    public string ScopeText =>
        Skill is null || Skill.Scope.Count == 0
            ? string.Empty
            : $"scope: {string.Join(", ", Skill.Scope)}";

    public string CommandText => Skill?.Command ?? string.Empty;

    public bool HasMetadata =>
        Skill is not null && (Skill.Scope.Count > 0 || !string.IsNullOrEmpty(Skill.Command));

    public string ModelLabel =>
        WorkNextOptionsDialogViewModel.Models.FirstOrDefault(m => m.Id == ModelId)?.Label
        ?? WorkNextOptionsDialogViewModel.Models[1].Label;

    partial void OnPanelWidthChanged(double value)
    {
        if (!_isLoadingPrefs && _workspaceId != Guid.Empty)
            _ = SavePrefsAsync();
    }

    public async Task LoadAsync(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        IsOpen = false;
        Skill = null;
        MarkdownBody = string.Empty;
        await LoadPrefsAsync();
    }

    public async Task OpenAsync(InstalledSkill skill)
    {
        Skill = skill;
        MarkdownBody = skill.MarkdownBody;
        var savedModel = await _appSettings.GetAsync($"skill.{skill.Name}.last_model")
                         ?? WorkNextOptionsDialogViewModel.DefaultModelId;
        ModelId = savedModel;
        IsOpen = true;
    }

    public async Task SetModelAsync(string modelId)
    {
        ModelId = modelId;
        if (Skill is not null)
            await _appSettings.SetAsync($"skill.{Skill.Name}.last_model", modelId);
    }

    public void SetAutoPanelWidth(double width)
    {
        _isLoadingPrefs = true;
        PanelWidth = Math.Max(MinPanelWidth, Math.Min(MaxPanelWidth, width));
        _isLoadingPrefs = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var path = Skill?.SourcePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var content = await File.ReadAllTextAsync(path);
            MarkdownBody = ExtractBody(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Bishop] SkillViewer.Refresh: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    internal static string ExtractBody(string content)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---") return string.Empty;

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() != "---") continue;
            return i + 1 < lines.Length
                ? string.Join("\n", lines.Skip(i + 1)).TrimStart('\n')
                : string.Empty;
        }
        return string.Empty;
    }

    private async Task LoadPrefsAsync()
    {
        if (!File.Exists(_prefsFilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(_prefsFilePath);
            var all = JsonSerializer.Deserialize<Dictionary<string, SkillViewerPrefs>>(json);
            if (all is null || !all.TryGetValue(_workspaceId.ToString(), out var prefs)) return;

            _isLoadingPrefs = true;
            PanelWidth = ClampWidth(prefs.PanelWidth);
            _isLoadingPrefs = false;
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] SkillViewer.LoadPrefs: {ex.Message}"); }
    }

    private async Task SavePrefsAsync()
    {
        if (_workspaceId == Guid.Empty) return;
        try
        {
            Dictionary<string, SkillViewerPrefs> all = [];
            if (File.Exists(_prefsFilePath))
            {
                var existing = await File.ReadAllTextAsync(_prefsFilePath);
                all = JsonSerializer.Deserialize<Dictionary<string, SkillViewerPrefs>>(existing) ?? [];
            }
            all[_workspaceId.ToString()] = new SkillViewerPrefs(PanelWidth);
            Directory.CreateDirectory(Path.GetDirectoryName(_prefsFilePath)!);
            await File.WriteAllTextAsync(_prefsFilePath, JsonSerializer.Serialize(all));
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] SkillViewer.SavePrefs: {ex.Message}"); }
    }

    private static double ClampWidth(double value) =>
        value < MinPanelWidth ? DefaultPanelWidth : Math.Min(value, MaxPanelWidth);

    private static string DefaultPrefsFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bishop.AI", "skill-viewer-prefs.json");

    private sealed record SkillViewerPrefs(double PanelWidth);
}
