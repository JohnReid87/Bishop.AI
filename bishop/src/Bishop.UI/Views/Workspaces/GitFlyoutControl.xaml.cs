using Bishop.ViewModels.Workspaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class GitFlyoutControl : UserControl
{
    public GitConfigViewModel Git { get; }
    public CommitsFlyoutViewModel Commits { get; }

    public event Action? RowCopied;

    public GitFlyoutControl(GitConfigViewModel git, CommitsFlyoutViewModel commits)
    {
        Git = git;
        Commits = commits;
        InitializeComponent();
    }

    private void CopyRemote_Click(object sender, RoutedEventArgs e)
    {
        var pkg = new DataPackage();
        pkg.SetText(Git.Remote);
        Clipboard.SetContent(pkg);
        RowCopied?.Invoke();
    }

    private void CommitRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommitRowViewModel row })
            Commits.RaiseCommitActivated(row);
    }
}
