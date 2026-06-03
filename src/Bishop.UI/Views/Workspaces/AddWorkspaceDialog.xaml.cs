using System.Threading.Tasks;
using Bishop.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class AddWorkspaceDialog : ContentDialog
{
    private readonly ISafeAsyncRunner _safeAsync;

    public AddWorkspaceDialogViewModel ViewModel { get; } = new();

    public AddWorkspaceDialog()
    {
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        InitializeComponent();
        Animations.EntranceAnimation.ApplyDialogEntrance(Content as Microsoft.UI.Xaml.FrameworkElement);
        IsPrimaryButtonEnabled = false;
        ViewModel.PropertyChanged += (_, _) =>
            IsPrimaryButtonEnabled = ViewModel.CanConfirm;
    }

    private async void BrowseExistingButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            var folder = await PickFolderAsync();
            if (folder is not null)
            {
                ViewModel.FolderPath = folder.Path;
                if (string.IsNullOrWhiteSpace(ViewModel.Name))
                    ViewModel.Name = folder.DisplayName;
            }
        });

    private async void BrowseParentButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            var folder = await PickFolderAsync();
            if (folder is not null)
                ViewModel.ParentFolderPath = folder.Path;
        });

    private static async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        return await picker.PickSingleFolderAsync();
    }
}
