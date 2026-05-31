using Microsoft.Extensions.Logging;

namespace Bishop.ViewModels.Shared;

public sealed class SafeAsyncRunner : ISafeAsyncRunner
{
    private readonly ILogger<SafeAsyncRunner> _logger;
    private readonly Action<Exception>? _onException;

    public SafeAsyncRunner(ILogger<SafeAsyncRunner> logger, Action<Exception>? onException = null)
    {
        _logger = logger;
        _onException = onException;
    }

    public async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in SafeAsyncRunner.RunAsync");
            _onException?.Invoke(ex);
        }
    }
}
