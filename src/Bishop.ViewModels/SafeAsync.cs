using Microsoft.Extensions.Logging;

namespace Bishop.ViewModels;

public static class SafeAsync
{
    public static Action<Exception>? OnException { get; set; }
    public static ILogger? Logger { get; set; }

    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unhandled exception in SafeAsync.RunAsync");
            OnException?.Invoke(ex);
        }
    }
}
