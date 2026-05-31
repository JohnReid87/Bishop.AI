namespace Bishop.ViewModels.Shared;

public interface ISafeAsyncRunner
{
    Task RunAsync(Func<Task> action);
}
