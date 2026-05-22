namespace Bishop.App.Tags;

public interface IDefaultTagSeeder
{
    Task EnsureAsync(string workspacePath, CancellationToken cancellationToken = default);
    Task EnsureAllAsync(CancellationToken cancellationToken = default);
}
