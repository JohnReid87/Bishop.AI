using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class LifeAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("starred")]
    public bool Starred { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    // Defaults to ThisWeek so pre-horizon files (no field) land in a sensible
    // bucket on read rather than the enum's zero value (Today).
    [JsonPropertyName("horizon")]
    public Horizon Horizon { get; set; } = Horizon.ThisWeek;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }
}
