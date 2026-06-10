using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bishop.Life.App.Plan;
using Bishop.Life.App.Speak;
using Bishop.Life.App.Standup;
using Bishop.Life.App.Web;
using Bishop.Life.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI;

namespace Bishop.Life.App;

/// <summary>
/// Bootstraps the WebView2 and dispatches inbound JS messages across the three
/// controllers extracted in cards #1069–#1071: <see cref="SpeakController"/>,
/// <see cref="StandupController"/>, and <see cref="PlanController"/>. Plan-file
/// load, mutation, and watcher reloads live in <see cref="PlanController"/>;
/// this host only handles bootstrap, theme, terminal launches, and routing.
/// </summary>
internal sealed class LifePlanHost : IDisposable
{
    private const string VirtualHost = "bishop.life";
    private const string LandingUrl = "https://bishop.life/index.html";

    private static readonly Color DarkBackground = Color.FromArgb(255, 0x14, 0x14, 0x14);
    private static readonly Color LightBackground = Color.FromArgb(255, 0xF3, 0xF3, 0xF3);

    private readonly WebView2 _view;
    private readonly DispatcherQueue _dispatcher;
    private readonly LifePlanFileService _service;
    private readonly LifePlanWatcher _watcher;
    private readonly LifeTerminalLauncher _launcher;
    private readonly LifeMutationCoordinator _coordinator;
    private SpeakController? _speak;
    private StandupController? _standup;
    private PlanController? _plan;
    private bool _disposed;
    // Set by MainWindow before EnsureCoreWebView2Async completes so the very
    // first paint of the WebView2 uses the right theme.
    private bool _pendingDarkTheme = true;

    public LifePlanHost(WebView2 view)
    {
        _view = view;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _service = new LifePlanFileService();
        _watcher = new LifePlanWatcher(_service.FilePath);
        _launcher = new LifeTerminalLauncher();
        _coordinator = new LifeMutationCoordinator(_service);
    }

    public async Task StartAsync()
    {
        // Suppress the white flash WebView2 paints before the first HTML frame
        // lands — has to land before EnsureCoreWebView2Async.
        _view.DefaultBackgroundColor = _pendingDarkTheme ? DarkBackground : LightBackground;

        await _view.EnsureCoreWebView2Async();

        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        _view.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, assetsDir, CoreWebView2HostResourceAccessKind.Allow);
        _view.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _view.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _view.CoreWebView2.Navigate(LandingUrl);

        _watcher.Start();

        var channel = new WebView2BrowserChannel(_view.CoreWebView2, _dispatcher);
        Action<Action> uiPost = action => _dispatcher.TryEnqueue(() => action());

        _plan = new PlanController(_service, _watcher, _coordinator, channel, uiPost);

        _speak = SpeakController.Create(channel);
        _speak.Start();

        _standup = new StandupController(
            ptyLauncher: _launcher.TryLaunchClaudePty,
            wtLauncher: (cwd, args) => _launcher.LaunchClaude(cwd, args),
            tailerFactory: StandupController.DefaultTailerFactory,
            channel: channel,
            uiPost: uiPost,
            scheduleAfter: ScheduleAfter);
        _standup.SessionEnded += OnStandupSessionEnded;
    }

    public void NoteWindowActivated() => _plan?.NoteWindowActivated();

    public void SetTheme(bool isDark)
    {
        _pendingDarkTheme = isDark;
        if (_view.CoreWebView2 is null) return;
        _view.DefaultBackgroundColor = isDark ? DarkBackground : LightBackground;
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // WebView2 raises this on the UI thread. Messages arrive as either a
        // bare string ("standup"/"init"/"add") or a JSON {type,…} object.
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;

            string? type;
            if (root.ValueKind == JsonValueKind.String) type = root.GetString();
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var typeEl)) type = typeEl.GetString();
            else return;

            switch (type)
            {
                case "standup": LaunchStandup(); break;
                case "init": LaunchInit(); break;
                case "add": LaunchAdd(); break;
                case "mutate" when root.TryGetProperty("plan", out var planEl): _plan?.ApplyMutation(planEl); break;
                case "terminal:input" when root.TryGetProperty("data", out var dataEl):
                    {
                        var submit = root.TryGetProperty("submit", out var submitEl) && submitEl.ValueKind == JsonValueKind.True;
                        _ = _standup?.HandleInputAsync(dataEl.GetString() ?? string.Empty, submit);
                        break;
                    }
                case "terminal:resize":
                    var cols = root.TryGetProperty("cols", out var colsEl) && colsEl.TryGetInt32(out var c) ? c : 0;
                    var rows = root.TryGetProperty("rows", out var rowsEl) && rowsEl.TryGetInt32(out var r) ? r : 0;
                    _standup?.Resize(cols, rows);
                    break;
            }
        }
        catch (JsonException ex)
        {
            // JS only ever sends well-formed envelopes; log so drift shows up.
            Debug.WriteLine($"LifePlanHost: malformed web message dropped: {ex.Message}");
        }
    }

    private void LaunchStandup()
    {
        if (_standup is null) return;
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);

        // Card #1059: pin a session id up front so the JSONL tailer can find the
        // on-disk transcript without a race against claude's first write.
        var sessionId = Guid.NewGuid().ToString();
        _standup.Launch(folder, $"/bish-life-standup --session-id {sessionId}", sessionId);
        _coordinator.NoteStandupLaunched(); // fires StateChanged → PlanController re-posts
    }

    private void LaunchInit()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        _launcher.LaunchClaude(folder, "/bish-life-init");
        // Init seeds the file from scratch; window activation on return refreshes.
    }

    private void LaunchAdd()
    {
        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        _launcher.LaunchClaude(folder, "/bish-life-add");
        _coordinator.NoteAddLaunched(); // fires StateChanged → PlanController re-posts
    }

    private void ScheduleAfter(TimeSpan delay, Action action)
    {
        var timer = _dispatcher.CreateTimer();
        timer.Interval = delay;
        timer.IsRepeating = false;
        timer.Tick += (s, _) =>
        {
            s.Stop();
            action();
        };
        timer.Start();
    }

    private void OnStandupSessionEnded() => _plan?.NoteStandupSessionEnded();

    private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args) =>
        _plan?.NotifyNavigated();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_standup is not null) _standup.SessionEnded -= OnStandupSessionEnded;
        _standup?.Dispose();
        _plan?.Dispose();
        _watcher.Dispose();
        _speak?.Dispose();
        if (_view.CoreWebView2 is { } core)
            core.WebMessageReceived -= OnWebMessageReceived;
    }
}
