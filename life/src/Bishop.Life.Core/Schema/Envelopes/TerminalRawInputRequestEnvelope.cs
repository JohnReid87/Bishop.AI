using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Viewer→host envelope: raw keystroke bytes from the debug-console overlay,
/// written straight to the PTY without passing through
/// <c>PtyInputSequencer</c> (no body/Enter split, no inter-write delay).
/// Discriminator: <c>terminal:raw-input</c>.
/// </summary>
public sealed record TerminalRawInputRequestEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] string Data);
