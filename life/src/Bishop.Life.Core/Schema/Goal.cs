using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class Goal
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("horizon")]
    public string? Horizon { get; set; }

    [JsonPropertyName("actions")]
    public List<LifeAction> Actions { get; set; } = new();
}
