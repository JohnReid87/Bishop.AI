using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Host→viewer envelope for transient system messages rendered into the stand-up
/// terminal pane. Discriminator: <c>terminal:systemNote</c>.
/// </summary>
public sealed record SystemNoteEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);
