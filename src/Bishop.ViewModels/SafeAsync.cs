namespace Bishop.ViewModels;

public static class SafeAsync
{
    public static Action<Exception>? OnException { get; set; }

    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            OnException?.Invoke(ex);
        }
    }
}
