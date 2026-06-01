using Bishop.ViewModels.Workspaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class CommitsFlyoutControl : UserControl
{
    public CommitsFlyoutViewModel ViewModel { get; }

    public CommitsFlyoutControl(CommitsFlyoutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void CommitRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommitRowViewModel row })
            ViewModel.RaiseCommitActivated(row);
    }
}
