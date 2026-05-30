using Bishop.ViewModels.Shared;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class AddWorkspaceDialog : ContentDialog
{
    public AddWorkspaceDialogViewModel ViewModel { get; } = new();

    public AddWorkspaceDialog()
    {
        InitializeComponent();
        IsPrimaryButtonEnabled = false;
        ViewModel.PropertyChanged += (_, _) =>
            IsPrimaryButtonEnabled = ViewModel.CanConfirm;
    }

    private async void BrowseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                ViewModel.FolderPath = folder.Path;
                if (string.IsNullOrWhiteSpace(ViewModel.Name))
                    ViewModel.Name = folder.DisplayName;
            }
        });
}
