namespace Bishop.ViewModels.Settings;

/// <summary>
/// Well-known keys for the DB-backed <c>IAppSettings</c> store. Centralised so the
/// writer (Settings dialog) and reader (workspace strip) can't drift on the string.
/// </summary>
internal static class AppSettingsKeys
{
    public const string ShowHiddenWorkspaces = "show_hidden_workspaces";
}
