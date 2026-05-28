using Bishop.App;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace Bishop.ViewModels;

public sealed partial class SettingsDialogViewModel : ObservableObject
{
    public string AppVersion { get; }
    public string DbPath { get; }
    public string BuildConfiguration { get; }
    public BishopSettingsViewModel Skills { get; }

    public SettingsDialogViewModel(BishopSettingsViewModel skills)
    {
        Skills = skills;

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
}
