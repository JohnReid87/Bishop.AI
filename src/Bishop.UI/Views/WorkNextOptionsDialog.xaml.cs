using Bishop.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class WorkNextOptionsDialog : ContentDialog
{
    public WorkNextOptionsDialogViewModel ViewModel { get; }

    public WorkNextOptionsDialog(IEnumerable<string> workspaceTagNames, string lastModelId = WorkNextOptionsDialogViewModel.DefaultModelId)
    {
        ViewModel = new WorkNextOptionsDialogViewModel(workspaceTagNames, lastModelId);
        InitializeComponent();
        IsPrimaryButtonEnabled = ViewModel.CanConfirm;
        ViewModel.PropertyChanged += (_, _) =>
            IsPrimaryButtonEnabled = ViewModel.CanConfirm;
    }
}
