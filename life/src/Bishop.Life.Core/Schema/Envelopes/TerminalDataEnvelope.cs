using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Host→viewer envelope: raw bytes from the stand-up PTY, fanned out
/// continuously from session start so the debug-console xterm can replay the
/// current TUI screen the moment it is opened. Discriminator: <c>terminal:data</c>.
/// </summary>
public sealed record TerminalDataEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] string Data);
