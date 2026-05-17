using Bishop.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Bishop.UI.Views;

public sealed partial class WorkspaceDetailPage : Page
{
    public WorkspaceDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is WorkspaceItemViewModel vm)
        {
            WorkspaceNameText.Text = vm.Name;
            WorkspacePathText.Text = vm.Path;
        }
    }
}
