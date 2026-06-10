using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.Core.Web;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;

namespace Bishop.Life.App.Web;

/// <summary>
/// Production <see cref="IBrowserChannel"/> wrapping
/// <see cref="CoreWebView2.PostWebMessageAsJson"/>. Serializes envelopes with the
/// shared camelCase options, then marshals onto the UI thread via the captured
/// <see cref="DispatcherQueue"/> so callers on background threads (pipe
/// listeners, PTY readers) can post without thinking about threading.
/// </summary>
internal sealed class WebView2BrowserChannel : IBrowserChannel
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly CoreWebView2 _view;
    private readonly DispatcherQueue _dispatcher;

    public WebView2BrowserChannel(CoreWebView2 view, DispatcherQueue dispatcher)
    {
        _view = view;
        _dispatcher = dispatcher;
    }

    public Task PostAsync(object envelope, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

        string json;
        try
        {
            json = JsonSerializer.Serialize(envelope, Options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2BrowserChannel: serialize failed: {ex.Message}");
            return Task.CompletedTask;
        }

        _dispatcher.TryEnqueue(() =>
        {
            try { _view.PostWebMessageAsJson(json); }
            catch (Exception ex) { Debug.WriteLine($"WebView2BrowserChannel: post failed: {ex.Message}"); }
        });
        return Task.CompletedTask;
    }
}
