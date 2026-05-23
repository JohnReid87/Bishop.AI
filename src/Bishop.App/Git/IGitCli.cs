using Bishop.App.Git.GetCardCommit;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;

namespace Bishop.App.Git;

public interface IGitCli
{
    Task<GetRecentCommitsResult> GetRecentCommitsAsync(string workspacePath, CancellationToken cancellationToken = default);

    Task<GetWorkingTreeStatusResult> GetWorkingTreeStatusAsync(string workspacePath, CancellationToken cancellationToken = default);

    Task<GetCardCommitResult> GetCardCommitAsync(int cardNumber, string workspacePath, CancellationToken cancellationToken = default);

    Task<string?> GetOriginUrlAsync(string workspacePath, CancellationToken cancellationToken = default);

    Task<PushResult> PushAsync(string workspacePath, CancellationToken cancellationToken = default);

    Task ResetHardAsync(string workspacePath, CancellationToken cancellationToken = default);

    Task CleanWorkingTreeAsync(string workspacePath, CancellationToken cancellationToken = default);
}
