namespace Bishop.App.Services.Terminal;

public interface IWorkspaceBootstrapper
{
    Task EnsureBootstrappedAsync(string workspacePath, CancellationToken cancellationToken = default);
}
