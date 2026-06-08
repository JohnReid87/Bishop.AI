using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema;

public sealed class InboxItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; set; }
}
