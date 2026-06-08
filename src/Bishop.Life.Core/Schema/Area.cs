using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class Area
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";

    [JsonPropertyName("goals")]
    public List<Goal> Goals { get; set; } = new();
}
