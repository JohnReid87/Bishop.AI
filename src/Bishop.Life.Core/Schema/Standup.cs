using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class Standup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("at")]
    public DateTimeOffset At { get; set; }

    [JsonPropertyName("reflection")]
    public string Reflection { get; set; } = "";

    [JsonPropertyName("focusToday")]
    public List<string> FocusToday { get; set; } = new();
}
