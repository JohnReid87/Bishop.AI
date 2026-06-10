using Bishop.App.Git.GetGitConfig;
using CommunityToolkit.Mvvm.ComponentModel;
using MediatR;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class GitConfigViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private bool _isGitRepo;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _remote = string.Empty;

    [ObservableProperty]
    private string _branch = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _identity = string.Empty;

    [ObservableProperty]
    private string _identityScope = string.Empty;

    public GitConfigViewModel(ISender mediator)
    {
        _mediator = mediator;
    }

    public async Task ProbeAsync(string workspacePath)
    {
        _workspacePath = workspacePath;
        IsGitRepo = false;
        IsLoaded = false;
        await RefreshAsync();
        IsLoaded = true;
    }

    public async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_workspacePath)) return;
        var result = await _mediator.Send(new GetGitConfigQuery(_workspacePath));
        if (result is GetGitConfigResult.Success s)
        {
            IsGitRepo = true;
            Apply(s);
        }
        else
        {
            IsGitRepo = false;
        }
    }

    private void Apply(GetGitConfigResult.Success s)
    {
        Remote = s.OriginUrl ?? "no remote";
        Branch = FormatBranch(s);
        Status = FormatStatus(s.StagedCount, s.UnstagedCount);
        (Identity, IdentityScope) = FormatIdentity(s);
    }

    private static string FormatBranch(GetGitConfigResult.Success s)
    {
        if (s.UpstreamRef is null)
            return $"{s.Branch} — no upstream";

        var line = $"{s.Branch} → {s.UpstreamRef}";
        if (s.Ahead == 0 && s.Behind == 0)
        {
            line += " (up to date)";
        }
        else
        {
            var parts = new List<string>();
            if (s.Ahead > 0) parts.Add($"{s.Ahead} ahead");
            if (s.Behind > 0) parts.Add($"{s.Behind} behind");
            line += $" ({string.Join(", ", parts)})";
        }
        if (!s.UpstreamIsTracked)
            line += " — no tracking";
        return line;
    }

    private static string FormatStatus(int staged, int unstaged)
    {
        if (staged == 0 && unstaged == 0) return "clean";
        var parts = new List<string>();
        if (staged > 0) parts.Add($"{staged} staged");
        if (unstaged > 0) parts.Add($"{unstaged} unstaged");
        return string.Join(", ", parts);
    }

    private static (string Identity, string Scope) FormatIdentity(GetGitConfigResult.Success s)
    {
        if (s.IdentityScope == GitIdentityScope.Unset || (s.Name is null && s.Email is null))
            return ("unset", string.Empty);

        var identity = $"{s.Name ?? "?"} <{s.Email ?? "?"}>";
        var scope = s.IdentityScope == GitIdentityScope.Repo ? "(repo)" : "(global)";
        return (identity, scope);
    }
}
