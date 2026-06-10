using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using Bishop.Life.Core.Web;

namespace Bishop.Life.App.Plan;

/// <summary>
/// Owns the plan-file concern previously inlined in <see cref="LifePlanHost"/>:
/// loads the plan via <see cref="LifePlanFileService"/>, posts envelope state to
/// the browser, applies inline mutations through <see cref="LifeMutationCoordinator"/>,
/// and refreshes on debounced disk changes from <see cref="LifePlanWatcher"/>.
/// Third slice of the LifePlanHost decomposition (card #1071); siblings are
/// <see cref="Speak.SpeakController"/> (card #1069) and
/// <see cref="Standup.StandupController"/> (card #1070).
/// The injected <see cref="IBrowserChannel"/> plus the <c>uiPost</c> delegate
/// keep the controller unit-testable without WebView2 or a real dispatcher.
/// </summary>
internal sealed class PlanController : IDisposable
{
    private static readonly JsonSerializerOptions PostOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly LifePlanFileService _service;
    private readonly LifePlanWatcher _watcher;
    private readonly LifeMutationCoordinator _coordinator;
    private readonly IBrowserChannel _channel;
    private readonly Action<Action> _uiPost;

    private bool _navigated;
    private bool _disposed;

    public PlanController(
        LifePlanFileService service,
        LifePlanWatcher watcher,
        LifeMutationCoordinator coordinator,
        IBrowserChannel channel,
        Action<Action> uiPost)
    {
        _service = service;
        _watcher = watcher;
        _coordinator = coordinator;
        _channel = channel;
        _uiPost = uiPost;

        _watcher.Reloaded += OnWatcherReloaded;
        _coordinator.StateChanged += OnCoordinatorStateChanged;
    }

    /// <summary>Absolute path of the plan file backing this controller.</summary>
    public string PlanFilePath => _service.FilePath;

    /// <summary>
    /// Called once the WebView2 has finished its first navigation. Until this
    /// fires, <see cref="PostState"/> is a no-op so envelopes don't race the
    /// landing page bootstrap.
    /// </summary>
    public void NotifyNavigated()
    {
        _navigated = true;
        PostState();
    }

    /// <summary>
    /// Belt-and-braces refresh on window activation — covers init completion
    /// (file went missing→ok), stand-up completion (file rewritten), and
    /// stand-up abort (terminal closed without a write). Clears any stuck
    /// in-flight flag.
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

    /// <summary>
    /// Wired from <see cref="Standup.StandupController.SessionEnded"/>. If the
    /// stand-up coordinator was still mid-flight the terminal exited without
    /// writing, so clear the flag; otherwise just re-post current state.
    /// </summary>
    public void NoteStandupSessionEnded()
    {
        if (_coordinator.StandupInFlight) _coordinator.NoteStandupAborted();
        else PostState();
    }

    /// <summary>
    /// Handles a <c>{"type":"mutate","plan":{…}}</c> envelope from JS: deserializes
    /// the plan, hands it to the coordinator (which performs the atomic save),
    /// and re-posts state so the browser sees the new on-disk shape.
    /// </summary>
    public void ApplyMutation(JsonElement planEl)
    {
        LifePlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<LifePlan>(planEl.GetRawText(), PostOptions);
        }
        catch (JsonException ex)
        {
            // JS only ever sends well-formed mutate envelopes; if this fires the
            // bridge contract has drifted. Log so it shows up in Debug Output, drop.
            Debug.WriteLine($"PlanController: malformed mutate payload dropped: {ex.Message}");
            return;
        }
        if (plan is null) return;

        _coordinator.ApplyMutation(plan);
        PostState();
    }

    /// <summary>
    /// Watcher fired — disk changed. The JS layer blocks inline mutations while
    /// a stand-up or add is in flight, so any reload event during in-flight is
    /// from the launched terminal (init seed, stand-up rewrite, or add append)
    /// and clears the flags. Public so tests can drive this path without
    /// touching the real <see cref="FileSystemWatcher"/>.
    /// </summary>
    internal void OnFileReloaded()
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
    }

    private void OnWatcherReloaded(object? sender, EventArgs e) =>
        _uiPost(OnFileReloaded);

    private void OnCoordinatorStateChanged(object? sender, EventArgs e) =>
        _uiPost(PostState);

    private void PostState()
    {
        if (!_navigated || _disposed) return;
        var envelope = BuildEnvelope();
        _ = _channel.PostAsync(envelope);
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
        _watcher.Reloaded -= OnWatcherReloaded;
    }

    internal sealed record Envelope(
        string Status,
        string FilePath,
        LifePlan? Plan,
        bool StandupInFlight,
        bool AddInFlight,
        string? Error = null);
}
