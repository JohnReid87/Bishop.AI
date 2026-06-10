using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Schema.Envelopes;

/// <summary>
/// Host→viewer envelope for the speak pipeline. Discriminator is either
/// <c>speak.started</c> (with PCM payload + duration) or <c>speak.stopped</c>
/// (payload fields null/zero).
/// </summary>
public sealed record SpeakEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("pcmBase64")] string? PcmBase64,
    [property: JsonPropertyName("pcmSampleRateHz")] int PcmSampleRateHz,
    [property: JsonPropertyName("durationMs")] int DurationMs);
