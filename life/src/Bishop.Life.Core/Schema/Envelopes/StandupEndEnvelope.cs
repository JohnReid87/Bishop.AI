using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Viewer→host signal that the user clicked "End Stand Up" on the topbar to
/// wrap the current session — kills the PTY immediately and hides the pane
/// without the post-exit delay or `[Claude session ended]` system note that
/// the natural-exit path uses. Discriminator: <c>standup:end</c>.
/// </summary>
public sealed record StandupEndEnvelope(
    [property: JsonPropertyName("type")] string Type);
