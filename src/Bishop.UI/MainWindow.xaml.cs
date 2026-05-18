using Bishop.UI.ViewModels;
using Bishop.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        WorkspacesListView.DragItemsCompleted += WorkspacesListView_DragItemsCompleted;

        _ = ViewModel.LoadAsync();
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

        if (await renameDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            item.Name = nameBox.Text;
            await ViewModel.RenameWorkspaceAsync(item);
        }
    }

    private async void DeleteWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WorkspaceItemViewModel item)
            return;

        var confirmDialog = new ContentDialog
        {
            Title = $"Delete \"{item.Name}\"?",
            Content = "This will remove the workspace from Bishop.AI. Your files will not be deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };

        if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteWorkspaceAsync(item);
    }
}
