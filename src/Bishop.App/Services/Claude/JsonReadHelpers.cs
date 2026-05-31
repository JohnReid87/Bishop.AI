using System.Text.Json;

namespace Bishop.App.Services.Claude;

internal static class JsonReadHelpers
{
    public static int ReadInt(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var prop)
            && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetInt32(out var v))
        {
            return v;
        }
        return 0;
    }

    public static decimal ReadDecimal(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var prop)
            && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetDecimal(out var v))
        {
            return v;
        }
        return 0m;
    }

    public static bool TryGetMessage(JsonElement root, out JsonElement message)
    {
        message = default;
        if (!root.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object)
            return false;
        message = m;
        return true;
    }

    public static bool TryGetContentArray(JsonElement message, out JsonElement content)
    {
        content = default;
        if (!message.TryGetProperty("content", out var c) || c.ValueKind != JsonValueKind.Array)
            return false;
        content = c;
        return true;
    }
}
