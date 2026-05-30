using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bishop.ViewModels.Shared;

public static class SafeAsync
{
    /// <summary>
    /// Invoked when <see cref="RunAsync"/> catches an unhandled exception.
    /// Must be set by <c>App.xaml.cs</c> before any <see cref="RunAsync"/> call executes.
    /// </summary>
    public static Action<Exception>? OnException { get; set; }

    /// <summary>
    /// Records unhandled exceptions caught by <see cref="RunAsync"/>.
    /// Must be set by <c>App.xaml.cs</c> (after <c>_host.Start()</c>) before any
    /// <see cref="RunAsync"/> call executes. If null when an exception occurs the exception
    /// is silently dropped — the <see cref="Debug.Assert"/> in the catch path will fire
    /// in debug builds to surface premature calls.
    /// </summary>
    public static ILogger? Logger { get; set; }

    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Debug.Assert(Logger is not null,
                "SafeAsync.Logger is null — App.xaml.cs must initialize SafeAsync.Logger before any RunAsync call.");
            Logger?.LogError(ex, "Unhandled exception in SafeAsync.RunAsync");
            OnException?.Invoke(ex);
        }
    }
}
