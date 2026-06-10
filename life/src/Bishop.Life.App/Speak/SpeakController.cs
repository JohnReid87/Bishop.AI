using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Bishop.Life.Core.Schema.Envelopes;
using Bishop.Life.Core.Speak;
using Bishop.Life.Core.Web;

namespace Bishop.Life.App.Speak;

/// <summary>
/// Owns the Speak concern previously inlined in <see cref="LifePlanHost"/>:
/// listens to <see cref="LifeSpeakPipeServer"/>, drives <see cref="LifeSpeakPlayer"/>
/// for WAV playback, and translates each message into a
/// <c>speak.&lt;kind&gt;</c> envelope posted through <see cref="IBrowserChannel"/>.
/// First slice of the LifePlanHost decomposition (card #1069). The
/// <see cref="IBrowserChannel"/> seam keeps the controller unit-testable
/// without WebView2.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class SpeakController : IDisposable
{
    private readonly LifeSpeakPipeServer _pipe;
    private readonly LifeSpeakPlayer _player;
    private readonly IBrowserChannel _channel;
    private bool _disposed;

    public SpeakController(LifeSpeakPipeServer pipe, LifeSpeakPlayer player, IBrowserChannel channel)
    {
        _pipe = pipe;
        _player = player;
        _channel = channel;
        _pipe.MessageReceived += OnPipeMessage;
    }

    /// <summary>
    /// Production factory — host code uses this so it doesn't need to know about
    /// the pipe-server or WAV-player types. Tests use the (pipe, player, channel)
    /// constructor directly.
    /// </summary>
    public static SpeakController Create(IBrowserChannel channel)
        => new(new LifeSpeakPipeServer(), new LifeSpeakPlayer(), channel);

    public void Start() => _pipe.Start();

    private void OnPipeMessage(SpeakPipeMessage message)
    {
        if (message.Kind == SpeakPipeMessage.KindStarted)
        {
            if (!string.IsNullOrEmpty(message.WavPath))
                _player.Start(message.WavPath);
        }
        else if (message.Kind == SpeakPipeMessage.KindStopped)
        {
            _player.Stop();
        }

        // Fire-and-forget: channel handles UI-thread marshalling internally.
        _ = HandleAsync(message);
    }

    internal Task HandleAsync(SpeakPipeMessage message)
    {
        var envelope = new SpeakEnvelope(
            Type: "speak." + message.Kind,
            PcmBase64: message.PcmBase64,
            PcmSampleRateHz: message.PcmSampleRateHz,
            DurationMs: message.DurationMs);
        return _channel.PostAsync(envelope);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.MessageReceived -= OnPipeMessage;
        _pipe.Dispose();
        _player.Dispose();
    }
}
