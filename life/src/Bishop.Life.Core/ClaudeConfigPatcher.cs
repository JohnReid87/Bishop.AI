using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bishop.Life.Core;

/// <summary>
/// Patches <c>~/.claude.json</c> in place to set the one-time
/// <c>bypassPermissionsModeAccepted</c> trust key, so PTY-driven stand-up
/// sessions don't stall on the invisible "are you sure?" trust dialog on
/// fresh machines (card #1085). All other content is preserved. Writes are
/// atomic via <c>.tmp</c> + rename. If the file doesn't exist, claude isn't
/// set up and the patcher is a no-op — the stand-up session can't run
/// anyway. If the key is already <c>true</c>, the file is left untouched.
/// </summary>
public static class ClaudeConfigPatcher
{
    public const string KeyName = "bypassPermissionsModeAccepted";

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude.json");

    public static void EnsureBypassPermissionsAccepted() =>
        EnsureBypassPermissionsAccepted(DefaultPath);

    public static void EnsureBypassPermissionsAccepted(string path)
    {
        if (!File.Exists(path)) return;

        var raw = File.ReadAllText(path);
        if (JsonNode.Parse(raw) is not JsonObject root) return;

        if (root.TryGetPropertyValue(KeyName, out var existing)
            && existing is JsonValue value
            && value.TryGetValue<bool>(out var current)
            && current)
        {
            return;
        }

        root[KeyName] = true;

        var tmp = path + LifePlanPaths.TempSuffix;
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tmp, json);
        File.Replace(tmp, path, destinationBackupFileName: null);
    }
}
