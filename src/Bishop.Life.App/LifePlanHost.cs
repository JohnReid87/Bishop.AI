using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Bishop.Life.App;

/// <summary>
/// Wires a <see cref="WebView2"/> to a bishop.life JSON file: loads the asset HTML
/// over a virtual host mapping, reads the plan via <see cref="LifePlanFileService"/>,
/// posts it to JS on load, and re-posts on every debounced file change via
/// <see cref="LifePlanWatcher"/>. Handles a single JS→host message — <c>"standup"</c>
/// — by launching Windows Terminal with <c>claude /bish-life-standup</c> in the
/// data-file folder; the file-watcher picks up any resulting edits.
/// </summary>
internal sealed class LifePlanHost : IDisposable
{
    private const string VirtualHost = "bishop.life";
    private const string LandingUrl = "https://bishop.life/index.html";
    private const string StandupCommand = "/bish-life-standup";

    private readonly WebView2 _view;
    private readonly DispatcherQueue _dispatcher;
    private readonly LifePlanFileService _service;
    private readonly LifePlanWatcher _watcher;
    private readonly LifeTerminalLauncher _launcher;
    private bool _navigated;
    private bool _disposed;

    private static readonly JsonSerializerOptions PostOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public LifePlanHost(WebView2 view)
    {
        _view = view;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _service = new LifePlanFileService();
        _watcher = new LifePlanWatcher(_service.FilePath);
        _watcher.Reloaded += OnFileReloaded;
        _launcher = new LifeTerminalLauncher();
    }

    public async Task StartAsync()
    {
        await _view.EnsureCoreWebView2Async();

        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        _view.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, assetsDir, CoreWebView2HostResourceAccessKind.Allow);

        _view.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _view.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _view.CoreWebView2.Navigate(LandingUrl);

        _watcher.Start();
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // WebView2 raises this on the UI thread; safe to call Process.Start directly.
        // index.html only ever posts string messages, so TryGetWebMessageAsString is safe.
        if (args.TryGetWebMessageAsString() != "standup") return;

        var folder = Path.GetDirectoryName(_service.FilePath);
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder); // ensure wt -d target exists on first run

        _launcher.LaunchClaude(folder, StandupCommand);
    }

    private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _navigated = true;
        PostState();
    }

    private void OnFileReloaded(object? sender, EventArgs e)
    {
        // Watcher fires on a thread-pool thread; PostWebMessage must run on the UI thread.
        _dispatcher.TryEnqueue(PostState);
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
        if (!_service.Exists())
            return new Envelope(Status: "missing", FilePath: _service.FilePath, Plan: null);

        try
        {
            var plan = _service.Load();
            return new Envelope(Status: "ok", FilePath: _service.FilePath, Plan: plan);
        }
        catch (Exception ex)
        {
            return new Envelope(Status: "error", FilePath: _service.FilePath, Plan: null, Error: ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.Reloaded -= OnFileReloaded;
        _watcher.Dispose();
        if (_view.CoreWebView2 is { } core)
            core.WebMessageReceived -= OnWebMessageReceived;
    }

    private sealed record Envelope(string Status, string FilePath, LifePlan? Plan, string? Error = null);
}
