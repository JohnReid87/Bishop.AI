using System.Text.Json;

namespace Bishop.App.Services.Claude;

internal static class SystemBlockFormatter
{
    public static string? Format(JsonElement root, StreamJsonFormatter state)
    {
        if (!root.TryGetProperty("subtype", out var subtypeProp)
            || subtypeProp.GetString() != "permission_denied")
            return null;

        if (state.OnDenial is null) return null;

        var tool = ReadStringProperty(root, "tool");
        var command = ExtractDeniedCommand(root);
        var message = ReadStringProperty(root, "message");

        state.OnDenial(new PermissionDeniedEvent(tool, command, message));
        return null;
    }

    private static string? ReadStringProperty(JsonElement root, string name)
        => root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? ExtractDeniedCommand(JsonElement root)
    {
        if (!root.TryGetProperty("toolInput", out var ti) || ti.ValueKind != JsonValueKind.Object)
            return null;
        return ti.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String
            ? cmd.GetString()
            : null;
    }
}
