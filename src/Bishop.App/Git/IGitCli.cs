namespace Bishop.App.Git;

public interface IGitCli
{
    Task<GetRecentCommitsResult> GetRecentCommitsAsync(string workspacePath, CancellationToken cancellationToken = default);
}
