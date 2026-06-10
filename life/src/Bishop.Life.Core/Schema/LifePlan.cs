using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class LifePlan
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "bishop.life/v1";

    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = new();

    [JsonPropertyName("areas")]
    public List<Area> Areas { get; set; } = new();

    [JsonPropertyName("inbox")]
    public List<InboxItem> Inbox { get; set; } = new();

    [JsonPropertyName("standups")]
    public List<Standup> Standups { get; set; } = new();
}
