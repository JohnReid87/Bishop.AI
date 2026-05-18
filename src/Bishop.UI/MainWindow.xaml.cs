using Bishop.UI.ViewModels;
using Bishop.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace Bishop.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;

        SetupTitleBar();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        WorkspacesListView.DragItemsCompleted += WorkspacesListView_DragItemsCompleted;

        _ = ViewModel.LoadAsync();
    }

    private void SetupTitleBar()
    {
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 120);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 30, 30, 38);
        AppWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 40, 40, 50);
        AppWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
        SetTitleBar(AppTitleBar);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedWorkspace))
        {
            if (ViewModel.SelectedWorkspace is { } selected)
                ContentFrame.Navigate(typeof(WorkspaceDetailPage), selected);
            else
                ContentFrame.Content = null;

            EmptyStateText.Visibility = ViewModel.EmptyStateVisibility;
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.EmptyStateVisibility))
        {
            EmptyStateText.Visibility = ViewModel.EmptyStateVisibility;
        }
    }

    private async void WorkspacesListView_DragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args)
    {
        var ordered = WorkspacesListView.Items
            .OfType<WorkspaceItemViewModel>()
            .ToList();
        await ViewModel.PersistReorderAsync(ordered);
    }

    private async void AddWorkspaceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddWorkspaceDialog { XamlRoot = Content.XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ViewModel.AddWorkspaceAsync(dialog.ViewModel);
    }

    private async void RenameWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WorkspaceItemViewModel item)
            return;

        var nameBox = new TextBox { Text = item.Name, SelectionStart = item.Name.Length };
        var renameDialog = new ContentDialog
        {
            Title = "Rename Workspace",
            Content = nameBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        renameDialog.Resources["ContentDialogBackground"] = Application.Current.Resources["AppSurfaceBrush"];

        if (await renameDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            item.Name = nameBox.Text;
            await ViewModel.RenameWorkspaceAsync(item);
        }
    }

    private void WorkspaceItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }

    private async void RepathWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WorkspaceItemViewModel item)
            return;

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            await ViewModel.RepathWorkspaceAsync(item, folder.Path);
    }

    private async void DeleteWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WorkspaceItemViewModel item)
            return;

        var confirmDialog = new ContentDialog
        {
            Title = $"Remove \"{item.Name}\"?",
            Content = "This will remove the workspace from Bishop.AI. Your files will not be affected.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        confirmDialog.Resources["ContentDialogBackground"] = Application.Current.Resources["AppSurfaceBrush"];

        if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteWorkspaceAsync(item);
    }
}
