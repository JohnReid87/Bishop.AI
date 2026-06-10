using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Viewer→host envelope: a keystroke (or paste) destined for the embedded
/// stand-up PTY. Discriminator: <c>terminal:input</c>. When <see cref="Submit"/>
/// is true the input sequencer appends Enter after the body.
/// </summary>
public sealed record TerminalInputRequestEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] string Data,
    [property: JsonPropertyName("submit")] bool Submit);
