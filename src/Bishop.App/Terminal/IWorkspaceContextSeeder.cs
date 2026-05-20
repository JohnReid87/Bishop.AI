namespace Bishop.App.Terminal;

public interface IWorkspaceContextSeeder
{
    Task SeedAsync(string workspacePath, CancellationToken cancellationToken = default);
}
