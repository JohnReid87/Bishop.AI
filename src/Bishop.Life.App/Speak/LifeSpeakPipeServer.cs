using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.Core.Speak;

namespace Bishop.Life.App.Speak;

/// <summary>
/// Hosts the named-pipe server end of <see cref="SpeakPipeContract"/>. Listens
/// on a background task, accepts one connection at a time (Piper synthesizes
/// utterances serially), parses NDJSON messages, and raises
/// <see cref="MessageReceived"/> for the host to play the WAV and forward
/// samples to the WebView2.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class LifeSpeakPipeServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private bool _disposed;

    public event Action<SpeakPipeMessage>? MessageReceived;

    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    SpeakPipeContract.PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8);
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    SpeakPipeMessage? msg;
                    try
                    {
                        msg = JsonSerializer.Deserialize<SpeakPipeMessage>(line, SpeakPipeContract.JsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"LifeSpeakPipeServer: malformed line dropped: {ex.Message}");
                        continue;
                    }
                    if (msg is not null) MessageReceived?.Invoke(msg);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Pipe errors (broken connection, permission, etc.) — log and
                // restart the listener so a crashed Cli doesn't take down the
                // viz host.
                Debug.WriteLine($"LifeSpeakPipeServer: connection failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
    }
}
