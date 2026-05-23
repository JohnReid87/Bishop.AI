namespace Bishop.App.Services.Terminal;

public interface IWorkspaceContextSeeder
{
    Task SeedAsync(string workspacePath, CancellationToken cancellationToken = default);
}
