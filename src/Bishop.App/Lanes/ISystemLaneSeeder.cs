namespace Bishop.App.Lanes;

public interface ISystemLaneSeeder
{
    Task EnsureAsync(string workspacePath, CancellationToken cancellationToken = default);
}
