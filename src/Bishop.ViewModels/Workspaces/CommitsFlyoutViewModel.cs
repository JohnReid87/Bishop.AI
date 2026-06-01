using Bishop.ViewModels.GitHub;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class CommitsFlyoutViewModel : ObservableObject
{
    private readonly BoardGitCoordinator _git;
    private readonly TimeProvider _timeProvider;
    private string _workspacePath = string.Empty;
    private string? _gitHubRepo;
    private bool _needsSetUpstream;

    public ObservableCollection<CommitRowViewModel> Commits { get; } = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasCommits;

    [ObservableProperty]
    private string _pushLabel = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PushCommand))]
    private bool _isPushEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PushCommand))]
    private bool _isPushing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPushError))]
    private string? _pushError;

    public bool HasPushError => PushError is not null;

    public event Action<CommitRowViewModel>? CommitActivated;

    public CommitsFlyoutViewModel(ISender mediator, TimeProvider timeProvider)
    {
        _git = new BoardGitCoordinator(mediator);
        _timeProvider = timeProvider;
    }

    public async Task LoadAsync(string workspacePath, string? gitHubRepo)
    {
        _workspacePath = workspacePath;
        _gitHubRepo = gitHubRepo;
        await RefreshAsync();
    }

    public void RaiseCommitActivated(CommitRowViewModel row) => CommitActivated?.Invoke(row);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var result = await _git.GetRecentCommitsAsync(_workspacePath);
        ApplyResult(result);
    }

    private bool CanPush() => IsPushEnabled && !IsPushing;

    [RelayCommand(CanExecute = nameof(CanPush))]
    private async Task PushAsync()
    {
        IsPushing = true;
        PushError = null;

        var outcome = await _git.PushAsync(_workspacePath, setUpstream: _needsSetUpstream);
        if (outcome.Success)
        {
            var refreshed = await _git.GetRecentCommitsAsync(_workspacePath);
            ApplyResult(refreshed);
        }
        else
        {
            PushError = string.IsNullOrWhiteSpace(outcome.Message)
                ? "Push failed"
                : $"Push failed: {outcome.Message}";
        }

        IsPushing = false;
    }

    private void ApplyResult(RecentCommitsResult result)
    {
        Commits.Clear();
        switch (result)
        {
            case RecentCommitsResult.Success s:
                var commits = s.Commits;
                for (var i = 0; i < commits.Count; i++)
                    Commits.Add(new CommitRowViewModel(commits[i], s.UpstreamRef, _gitHubRepo, _timeProvider, showSeparator: i < commits.Count - 1));
                HasCommits = true;
                ApplyPushState(s.UpstreamRef, s.UpstreamIsTracked, s.UnpushedCount);
                break;
            case RecentCommitsResult.NotAGitRepo:
                HasCommits = false;
                IsPushEnabled = false;
                StatusMessage = "Not a git repository";
                break;
            case RecentCommitsResult.GitNotFound:
                HasCommits = false;
                IsPushEnabled = false;
                StatusMessage = "Git not installed or not on PATH";
                break;
            case RecentCommitsResult.NoCommits:
                HasCommits = false;
                IsPushEnabled = false;
                StatusMessage = "No commits yet";
                break;
        }
    }

    private void ApplyPushState(string? upstreamRef, bool upstreamIsTracked, int unpushedCount)
    {
        if (upstreamRef is null)
        {
            PushLabel = "No remote branch — push with -u to publish";
            IsPushEnabled = false;
            _needsSetUpstream = true;
        }
        else if (unpushedCount == 0)
        {
            PushLabel = "Up to date";
            IsPushEnabled = false;
            _needsSetUpstream = false;
        }
        else
        {
            PushLabel = upstreamIsTracked
                ? $"Push {unpushedCount} commit{(unpushedCount == 1 ? "" : "s")}"
                : $"Push {unpushedCount} commit{(unpushedCount == 1 ? "" : "s")} (will set upstream)";
            IsPushEnabled = true;
            _needsSetUpstream = !upstreamIsTracked;
        }
    }
}
