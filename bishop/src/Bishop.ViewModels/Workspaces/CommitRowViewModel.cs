namespace Bishop.ViewModels.Workspaces;

public sealed class CommitRowViewModel
{
    public string ShortHash { get; }
    public string FullHash { get; }
    public string Subject { get; }
    public string RelativeTime { get; }
    public bool IsIconHighlighted { get; }
    public string IconTooltip { get; }
    public string Tooltip { get; }
    public bool ShowSeparator { get; }

    public CommitRowViewModel(
        Git.CommitItem commit,
        string? upstreamRef,
        TimeProvider timeProvider,
        bool showSeparator)
    {
        ShortHash = commit.ShortHash;
        FullHash = commit.FullHash;
        Subject = commit.Subject;
        RelativeTime = FormatRelativeTime(commit.Timestamp, timeProvider);
        IsIconHighlighted = commit.IsPushed && upstreamRef is not null;
        ShowSeparator = showSeparator;

        IconTooltip = upstreamRef is null ? "No remote branch"
            : commit.IsPushed ? "Pushed"
            : "Not yet pushed";

        var tooltipText = string.IsNullOrEmpty(commit.Body)
            ? commit.Subject
            : $"{commit.Subject}\n\n{commit.Body}";
        if (commit.IsPushed && upstreamRef is not null)
            tooltipText += $"\n\nPushed to {upstreamRef}";
        Tooltip = tooltipText;
    }

    private static string FormatRelativeTime(DateTimeOffset timestamp, TimeProvider timeProvider)
    {
        var elapsed = timeProvider.GetUtcNow() - timestamp.ToUniversalTime();
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"{(int)elapsed.TotalDays}d ago";
        return $"{(int)(elapsed.TotalDays / 30)}mo ago";
    }
}
