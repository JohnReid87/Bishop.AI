using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Type-only host→viewer envelope. Discriminator covers signals with no payload —
/// currently <c>terminal:show</c> and <c>terminal:hide</c>.
/// </summary>
public sealed record BareEnvelope(
    [property: JsonPropertyName("type")] string Type);
