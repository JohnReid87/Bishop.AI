using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class Meta
{
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("lastStandupAt")]
    public DateTimeOffset? LastStandupAt { get; set; }
}
