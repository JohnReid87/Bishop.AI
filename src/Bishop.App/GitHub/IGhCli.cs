namespace Bishop.App.GitHub;

public interface IGhCli
{
    Task RunAsync(string[] args, CancellationToken cancellationToken = default);
    Task<string> RunCaptureAsync(string[] args, CancellationToken cancellationToken = default);
}
