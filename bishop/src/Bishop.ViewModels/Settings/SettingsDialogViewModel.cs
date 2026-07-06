using Bishop.App;
using Bishop.App.Services.Settings;
using Bishop.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace Bishop.ViewModels.Settings;

public sealed partial class SettingsDialogViewModel : ObservableObject
{
    private readonly IAppSettings _appSettings;
    private readonly ISafeAsyncRunner _safeAsync;

    // Guards the initial load: applying the persisted value must not write it back.
    private bool _suppressShowHiddenSave;
    private bool _suppressShowClosedBatchesSave;

    public string AppVersion { get; }
    public string DbPath { get; }
    public string BuildConfiguration { get; }
    public BishopSettingsViewModel Skills { get; }

    [ObservableProperty]
    public partial bool ShowHiddenWorkspaces { get; set; }

    [ObservableProperty]
    public partial bool ShowClosedBatches { get; set; }

    public SettingsDialogViewModel(BishopSettingsViewModel skills, IAppSettings appSettings, ISafeAsyncRunner safeAsync)
    {
        Skills = skills;
        _appSettings = appSettings;
        _safeAsync = safeAsync;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version is null ? "—" : $"{version.Major}.{version.Minor}.{version.Build}";

        var connStr = BishopDbConnectionString.Resolve();
        DbPath = connStr.StartsWith("Data Source=", StringComparison.Ordinal)
            ? connStr["Data Source=".Length..]
            : connStr;

#if DEBUG
        BuildConfiguration = "Debug";
#else
        BuildConfiguration = "Release";
#endif
    }

    /// <summary>
    /// Loads the persisted General-tab settings so the toggle reflects the stored value
    /// on open. Applied under a guard so seeding the property doesn't re-persist it.
    /// </summary>
    public async Task LoadGeneralAsync()
    {
        var rawShowHidden = await _appSettings.GetAsync(AppSettingsKeys.ShowHiddenWorkspaces);
        _suppressShowHiddenSave = true;
        ShowHiddenWorkspaces = bool.TryParse(rawShowHidden, out var showHidden) && showHidden;
        _suppressShowHiddenSave = false;

        var rawShowClosed = await _appSettings.GetAsync(AppSettingsKeys.ShowClosedBatches);
        _suppressShowClosedBatchesSave = true;
        ShowClosedBatches = bool.TryParse(rawShowClosed, out var showClosed) && showClosed;
        _suppressShowClosedBatchesSave = false;
    }

    partial void OnShowHiddenWorkspacesChanged(bool value)
    {
        if (_suppressShowHiddenSave)
            return;
        _ = _safeAsync.RunAsync(() =>
            _appSettings.SetAsync(AppSettingsKeys.ShowHiddenWorkspaces, value.ToString()));
    }

    partial void OnShowClosedBatchesChanged(bool value)
    {
        if (_suppressShowClosedBatchesSave)
            return;
        _ = _safeAsync.RunAsync(() =>
            _appSettings.SetAsync(AppSettingsKeys.ShowClosedBatches, value.ToString()));
    }
}
