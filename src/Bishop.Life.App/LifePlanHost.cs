using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bishop.Life.App.Speak;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using Bishop.Life.Core.Speak;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI;

namespace Bishop.Life.App;

/// <summary>
/// Wires a <see cref="WebView2"/> to a bishop.life JSON file: loads the asset HTML
/// over a virtual host mapping, reads the plan via <see cref="LifePlanFileService"/>,
/// and posts envelope state to JS. State is refreshed from disk on every debounced
/// disk change via <see cref="LifePlanWatcher"/>, with <see cref="NoteWindowActivated"/>
/// as a belt-and-braces refresh for cases where the watcher is late or the user
/// returns from a launched terminal that wrote nothing.
/// Handles two JS→host message shapes:
/// <list type="bullet">
///   <item><c>"standup"</c> — launches Windows Terminal with <c>claude /bish-life-standup</c>.</item>
///   <item><c>{"type":"mutate","plan":{…}}</c> — applies an inline edit (star,
///         check, title) via <see cref="LifeMutationCoordinator"/>.</item>
/// </list>
/// </summary>
internal sealed class LifePlanHost : IDisposable
{
    private const string VirtualHost = "bishop.life";
    private const string LandingUrl = "https://bishop.life/index.html";
    private const string StandupCommand = "/bish-life-standup";
    private const string InitCommand = "/bish-life-init";
    private const string AddCommand = "/bish-life-add";

    private static readonly Color DarkBackground = Color.FromArgb(255, 0x14, 0x14, 0x14);
    private static readonly Color LightBackground = Color.FromArgb(255, 0xF3, 0xF3, 0xF3);

    private readonly WebView2 _view;
    private readonly DispatcherQueue _dispatcher;
    private readonly LifePlanFileService _service;
    private readonly LifePlanWatcher _watcher;
    private readonly LifeTerminalLauncher _launcher;
    private readonly LifeMutationCoordinator _coordinator;
    private readonly LifeSpeakPipeServer _speakPipe;
    private readonly LifeSpeakPlayer _speakPlayer;
    private bool _navigated;
    private bool _disposed;
    // Set by MainWindow before EnsureCoreWebView2Async completes so the very
    // first paint of the WebView2 uses the right theme.
    private bool _pendingDarkTheme = true;

    private static readonly JsonSerializerOptions PostOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public LifePlanHost(WebView2 view)
    {
        _view = view;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _service = new LifePlanFileService();
        _watcher = new LifePlanWatcher(_service.FilePath);
        _watcher.Reloaded += OnFileReloaded;
        _launcher = new LifeTerminalLauncher();
        _coordinator = new LifeMutationCoordinator(_service);
        _coordinator.StateChanged += OnCoordinatorStateChanged;
        _speakPlayer = new LifeSpeakPlayer();
        _speakPipe = new LifeSpeakPipeServer();
        _speakPipe.MessageReceived += OnSpeakMessage;
    }

    public async Task StartAsync()
    {
        // Suppress the white flash that WebView2 paints before the first HTML
        // frame lands. Has to land before EnsureCoreWebView2Async or the bare
        // CoreWebView2 surface paints white for a frame on first show.
        _view.DefaultBackgroundColor = _pendingDarkTheme ? DarkBackground : LightBackground;

        await _view.EnsureCoreWebView2Async();

        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        _view.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, assetsDir, CoreWebView2HostResourceAccessKind.Allow);

        _view.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _view.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _view.CoreWebView2.Navigate(LandingUrl);

        _watcher.Start();
        _speakPipe.Start();
    }

    private void OnSpeakMessage(SpeakPipeMessage message)
    {
        // Pipe events arrive on a background thread; bounce to the UI thread
        // so we can touch the WebView2 safely. SoundPlayer is happy on either.
        if (message.Kind == SpeakPipeMessage.KindStarted)
        {
            if (!string.IsNullOrEmpty(message.WavPath))
                _speakPlayer.Start(message.WavPath);
        }
        else if (message.Kind == SpeakPipeMessage.KindStopped)
        {
            _speakPlayer.Stop();
        }

        _dispatcher.TryEnqueue(() => PostSpeakMessage(message));
    }

    private void PostSpeakMessage(SpeakPipeMessage message)
    {
        if (!_navigated || _disposed) return;
        var payload = new SpeakEnvelope(
            Type: "speak." + message.Kind,
            Samples: message.Samples,
            SampleRateHz: message.SampleRateHz,
            DurationMs: message.DurationMs);
        var json = JsonSerializer.Serialize(payload, PostOptions);
        _view.CoreWebView2.PostWebMessageAsJson(json);
    }

    /// <summary>
    /// Called by the host window on activation. Pushes a fresh envelope so the
    /// WebView2 reflects current disk state — covers init completion (file went
    /// missing→ok), stand-up completion (file rewritten), and stand-up abort
    /// (terminal closed without a write). Also clears any stuck in-flight flag.
    /// </summary>
    public void NoteWindowActivated()
    {
        if (!_navigated || _disposed) return;
        var cleared = false;
        if (_coordinator.StandupInFlight)
        {
            _coordinator.NoteStandupAborted(); // fires StateChanged → PostState
            cleared = true;
        }
        if (_coordinator.AddInFlight)
        {
            _coordinator.NoteAddAborted();
            cleared = true;
        }
        if (!cleared) PostState();
    }

    public void SetTheme(bool isDark)
    {
        _pendingDarkTheme = isDark;
        if (_view.CoreWebView2 is null) return;
        _view.DefaultBackgroundColor = isDark ? DarkBackground : LightBackground;
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // WebView2 raises this on the UI thread. Messages arrive in two shapes —
        // a bare "standup" string (legacy path) or a JSON object {type,plan}.
        var json = args.WebMessageAsJson; // always JSON-encoded, even for strings
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                var s = root.GetString();
                if (s == "standup") LaunchStandup();
                else if (s == "init") LaunchInit();
                else if (s == "add") LaunchAdd();
                return;
            }

            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();

            if (type == "standup")
            {
                LaunchStandup();
            }
            else if (type == "init")
            {
                LaunchInit();
            }
            else if (type == "add")
            {
                LaunchAdd();
            }
            else if (type == "mutate" && root.TryGetProperty("plan", out var planEl))
            {
                ApplyMutation(planEl);
            }
        }
        catch (JsonException ex)
        {
            // JS only ever sends well-formed envelopes; if this fires the bridge
            // contract has drifted. Log so it shows up in Debug Output, then drop.
            Debug.WriteLine($"LifePlanHost: malformed web message dropped: {ex.Message}");
        }
    }

    private void LaunchStandup()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder); // ensure wt -d target exists on first run

        _launcher.LaunchClaude(folder, StandupCommand);
        _coordinator.NoteStandupLaunched(); // fires StateChanged → PostState
    }

    private void LaunchInit()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);

        _launcher.LaunchClaude(folder, InitCommand);
        // Init seeds the file from scratch; window activation on return refreshes
        // the envelope.
    }

    private void LaunchAdd()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);

        _launcher.LaunchClaude(folder, AddCommand);
        _coordinator.NoteAddLaunched(); // fires StateChanged → PostState
    }

    private void ApplyMutation(JsonElement planEl)
    {
        LifePlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<LifePlan>(planEl.GetRawText(), PostOptions);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"LifePlanHost: malformed mutate payload dropped: {ex.Message}");
            return;
        }
        if (plan is null) return;

        _coordinator.ApplyMutation(plan);
        PostState();
    }

    private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _navigated = true;
        PostState();
    }

    private void OnCoordinatorStateChanged(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(PostState);
    }

    /// <summary>
    /// Watcher fired — disk changed. The JS layer blocks inline mutations while a
    /// stand-up or add is in flight, so any reload event during in-flight is from
    /// the launched terminal (init seed, stand-up rewrite, or add append) and
    /// clears the flags. On the UI thread because <see cref="LifeMutationCoordinator"/>
    /// isn't thread-safe.
    /// </summary>
    private void OnFileReloaded(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_disposed) return;
            var cleared = false;
            if (_coordinator.StandupInFlight)
            {
                _coordinator.NoteStandupAborted(); // fires StateChanged → PostState
                cleared = true;
            }
            if (_coordinator.AddInFlight)
            {
                _coordinator.NoteAddAborted();
                cleared = true;
            }
            if (!cleared) PostState();
        });
    }

    private void PostState()
    {
        if (!_navigated || _disposed) return;

        var envelope = BuildEnvelope();
        var json = JsonSerializer.Serialize(envelope, PostOptions);
        _view.CoreWebView2.PostWebMessageAsJson(json);
    }

    private Envelope BuildEnvelope()
    {
        var standupInFlight = _coordinator.StandupInFlight;
        var addInFlight = _coordinator.AddInFlight;

        if (!_service.Exists())
            return new Envelope(Status: "missing", FilePath: _service.FilePath, Plan: null, StandupInFlight: standupInFlight, AddInFlight: addInFlight);

        try
        {
            var plan = _service.Load();
            return new Envelope(Status: "ok", FilePath: _service.FilePath, Plan: plan, StandupInFlight: standupInFlight, AddInFlight: addInFlight);
        }
        catch (Exception ex)
        {
            return new Envelope(Status: "error", FilePath: _service.FilePath, Plan: null, StandupInFlight: standupInFlight, AddInFlight: addInFlight, Error: ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
        _watcher.Reloaded -= OnFileReloaded;
        _watcher.Dispose();
        _speakPipe.MessageReceived -= OnSpeakMessage;
        _speakPipe.Dispose();
        _speakPlayer.Dispose();
        if (_view.CoreWebView2 is { } core)
            core.WebMessageReceived -= OnWebMessageReceived;
    }

    private sealed record SpeakEnvelope(
        string Type,
        float[]? Samples,
        int SampleRateHz,
        int DurationMs);

    private sealed record Envelope(
        string Status,
        string FilePath,
        LifePlan? Plan,
        bool StandupInFlight,
        bool AddInFlight,
        string? Error = null);
}
