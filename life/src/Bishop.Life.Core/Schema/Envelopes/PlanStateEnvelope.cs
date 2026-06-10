using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Host→viewer envelope carrying the loaded plan plus coordinator in-flight flags.
/// No <c>type</c> discriminator — the viewer dispatcher treats anything without
/// a recognised <c>type</c> as a plan-state envelope (see <c>dispatcher.js</c>).
/// </summary>
public sealed record PlanStateEnvelope(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("plan")] LifePlan? Plan,
    [property: JsonPropertyName("standupInFlight")] bool StandupInFlight,
    [property: JsonPropertyName("addInFlight")] bool AddInFlight,
    [property: JsonPropertyName("error")] string? Error = null);
