using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Speak;

/// <summary>
/// IPC contract between <c>Bishop.Cli</c> (Piper TTS publisher) and
/// <c>Bishop.Life.App</c> (visualisation host) over the named pipe
/// <see cref="PipeName"/>. NDJSON: one <see cref="SpeakPipeMessage"/> per
/// line, UTF-8. The client owns synthesis; the server owns playback and
/// forwarding the raw PCM payload to the WebView2 viz panel.
/// </summary>
public static class SpeakPipeContract
{
    public const string PipeName = "bishop-life-speak";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}

/// <summary>
/// Message kinds the contract carries. <c>Started</c> opens an utterance and
/// carries the WAV path the server should play plus a base64-encoded mono
/// Int16 PCM buffer (little-endian) the viz uses to draw a real-waveform
/// oscilloscope trace. <c>Stopped</c> marks utterance end so the viz returns
/// to idle. Raw PCM is sent (not pre-computed bins) so swapping
/// scope ↔ FFT ↔ future visuals is a JS change with no C# rebuild.
/// </summary>
public sealed record SpeakPipeMessage
{
    public required string Kind { get; init; }
    public string? WavPath { get; init; }
    public string? PcmBase64 { get; init; }
    public int PcmSampleRateHz { get; init; }
    public int DurationMs { get; init; }

    public const string KindStarted = "started";
    public const string KindStopped = "stopped";
}
