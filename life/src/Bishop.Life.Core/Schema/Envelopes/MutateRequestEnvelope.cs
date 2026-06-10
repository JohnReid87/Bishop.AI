using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Viewer→host envelope: inline plan mutation. Discriminator: <c>mutate</c>.
/// The host deserializes the payload via JsonElement today; this record exists
/// so the wire contract is statically described on both sides (schema.d.ts).
/// </summary>
public sealed record MutateRequestEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("plan")] LifePlan Plan);
