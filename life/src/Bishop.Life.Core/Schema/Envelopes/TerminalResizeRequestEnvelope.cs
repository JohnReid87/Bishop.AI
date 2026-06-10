using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Viewer→host envelope: terminal viewport resize. Discriminator:
/// <c>terminal:resize</c>. Sub-1 dimensions are ignored host-side so the cached
/// defaults aren't poisoned by a zero-sized layout pass.
/// </summary>
public sealed record TerminalResizeRequestEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("cols")] int Cols,
    [property: JsonPropertyName("rows")] int Rows);
