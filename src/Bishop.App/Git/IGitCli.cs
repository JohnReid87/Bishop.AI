namespace Bishop.App.Git;

public interface IGitCli
{
    Task<GetRecentCommitsResult> GetRecentCommitsAsync(string workspacePath, CancellationToken cancellationToken = default);

    Task<GetWorkingTreeStatusResult> GetWorkingTreeStatusAsync(string workspacePath, CancellationToken cancellationToken = default);
}
