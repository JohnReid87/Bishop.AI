using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Host→viewer envelope fanning out lines from the Claude JSONL transcript tailer.
/// Discriminator: <c>transcript:event</c>. <see cref="Kind"/> is one of
/// <c>user</c>, <c>assistant</c>, <c>tool</c>.
/// </summary>
public sealed record TranscriptEventEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("text")] string Text);
