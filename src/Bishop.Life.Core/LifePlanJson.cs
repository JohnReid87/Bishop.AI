using System.Text.Encodings.Web;
using System.Text.Json;
using Bishop.Life.Core.Schema;

namespace Bishop.Life.Core;

public static class LifePlanJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public static string Serialize(LifePlan plan) =>
        JsonSerializer.Serialize(plan, Options);

    public static LifePlan Deserialize(string json) =>
        JsonSerializer.Deserialize<LifePlan>(json, Options)
            ?? throw new InvalidOperationException("Deserialized LifePlan was null.");
}
