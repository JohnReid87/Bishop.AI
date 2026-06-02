using Bishop.ViewModels.Workspaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class GitConfigFlyoutControl : UserControl
{
    public GitConfigViewModel ViewModel { get; }

    public event Action? RowCopied;

    public GitConfigFlyoutControl(GitConfigViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void CopyRemote_Click(object sender, RoutedEventArgs e) => Copy(ViewModel.Remote);

    private void Copy(string value)
    {
        var pkg = new DataPackage();
        pkg.SetText(value);
        Clipboard.SetContent(pkg);
        RowCopied?.Invoke();
    }
}
