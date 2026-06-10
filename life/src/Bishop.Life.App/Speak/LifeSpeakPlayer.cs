using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.Versioning;

namespace Bishop.Life.App.Speak;

/// <summary>
/// Plays a WAV file by copying its bytes into memory first, then handing the
/// in-memory stream to <see cref="SoundPlayer"/>. The copy is the trust
/// boundary with the Cli publisher: once we return from Start, Cli is free
/// to delete the temp WAV on its <c>finally</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class LifeSpeakPlayer : IDisposable
{
    private SoundPlayer? _current;
    private MemoryStream? _currentStream;
    private readonly object _gate = new();

    public void Start(string wavPath)
    {
        if (!File.Exists(wavPath))
        {
            Debug.WriteLine($"LifeSpeakPlayer: wav missing: {wavPath}");
            return;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(wavPath); }
        catch (Exception ex)
        {
            Debug.WriteLine($"LifeSpeakPlayer: read failed: {ex.Message}");
            return;
        }

        lock (_gate)
        {
            StopLocked();
            _currentStream = new MemoryStream(bytes, writable: false);
            _current = new SoundPlayer(_currentStream);
            try { _current.Play(); }
            catch (Exception ex) { Debug.WriteLine($"LifeSpeakPlayer: play failed: {ex.Message}"); }
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    private void StopLocked()
    {
        try { _current?.Stop(); } catch { }
        _current?.Dispose();
        _currentStream?.Dispose();
        _current = null;
        _currentStream = null;
    }

    public void Dispose() => Stop();
}
