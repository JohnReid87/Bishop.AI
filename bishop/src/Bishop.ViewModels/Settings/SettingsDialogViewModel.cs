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

    public string AppVersion { get; }
    public string DbPath { get; }
    public string BuildConfiguration { get; }
    public BishopSettingsViewModel Skills { get; }

    [ObservableProperty]
    public partial bool ShowHiddenWorkspaces { get; set; }

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
        var raw = await _appSettings.GetAsync(AppSettingsKeys.ShowHiddenWorkspaces);
        _suppressShowHiddenSave = true;
        ShowHiddenWorkspaces = bool.TryParse(raw, out var value) && value;
        _suppressShowHiddenSave = false;
    }

    partial void OnShowHiddenWorkspacesChanged(bool value)
    {
        if (_suppressShowHiddenSave)
            return;
        _ = _safeAsync.RunAsync(() =>
            _appSettings.SetAsync(AppSettingsKeys.ShowHiddenWorkspaces, value.ToString()));
    }
}
