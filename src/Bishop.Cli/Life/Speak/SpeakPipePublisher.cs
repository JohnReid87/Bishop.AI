using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Bishop.Life.Core.Speak;

namespace Bishop.Cli.Life.Speak;

/// <summary>
/// Tries to hand off a Piper-synthesized WAV to a Life.App listener over the
/// named pipe in <see cref="SpeakPipeContract"/>. On success the listener owns
/// playback and viz; the caller skips local <c>SoundPlayer.PlaySync()</c>. On
/// connect failure the caller falls back to today's local playback unchanged.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SpeakPipePublisher
{
    private const int AmplitudesPerSecond = 40;
    private const int ConnectTimeoutMs = 200;

    public static async Task<bool> TryPublishAsync(string wavPath, CancellationToken cancellationToken)
    {
        WavAmplitudeReader.Envelope envelope;
        try
        {
            envelope = WavAmplitudeReader.Read(wavPath, AmplitudesPerSecond);
        }
        catch (Exception)
        {
            return false;
        }

        NamedPipeClientStream? client = null;
        try
        {
            client = new NamedPipeClientStream(".", SpeakPipeContract.PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(ConnectTimeoutMs, cancellationToken);
        }
        catch (TimeoutException)
        {
            client?.Dispose();
            return false;
        }
        catch (Exception)
        {
            client?.Dispose();
            return false;
        }

        try
        {
            var started = new SpeakPipeMessage
            {
                Kind = SpeakPipeMessage.KindStarted,
                WavPath = wavPath,
                Samples = envelope.Samples,
                SampleRateHz = AmplitudesPerSecond,
                DurationMs = envelope.DurationMs,
            };
            await WriteLineAsync(client, started, cancellationToken);

            // Hold the WAV on disk for the playback duration plus a small
            // buffer so the listener has time to read it into memory. The
            // listener should copy bytes immediately on `started` rather
            // than streaming off disk to keep this margin generous.
            var holdMs = envelope.DurationMs + 500;
            try
            {
                await Task.Delay(holdMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down — still try to signal stop cleanly so the
                // listener's viz returns to idle.
                System.Diagnostics.Debug.WriteLine("SpeakPipePublisher: cancelled during hold; will still send stopped.");
            }

            var stopped = new SpeakPipeMessage { Kind = SpeakPipeMessage.KindStopped };
            await WriteLineAsync(client, stopped, CancellationToken.None);
            return true;
        }
        catch (Exception)
        {
            return true; // Listener was reachable; don't double-play locally.
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task WriteLineAsync(Stream stream, SpeakPipeMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, SpeakPipeContract.JsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }
}
