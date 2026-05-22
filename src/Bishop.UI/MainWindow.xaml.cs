using Bishop.UI.ViewModels;
using Bishop.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
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
        ApplyWindowGeometry();
        Closed += (_, _) => SaveWindowGeometry();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

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

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "bishop-ai.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        SetTitleBar(AppTitleBar);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedWorkspace))
        {
            // CanDragItems clears the ListView selection during drag; suppress navigation until drag ends.
            if (ViewModel.SelectedWorkspace is null && _selectionBeforeDrag is not null)
                return;

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

    private WorkspaceItemViewModel? _draggedWorkspace;
    private WorkspaceItemViewModel? _selectionBeforeDrag;

    private void WorkspaceDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        _draggedWorkspace = e.Items.OfType<WorkspaceItemViewModel>().FirstOrDefault();
        _selectionBeforeDrag = ViewModel.SelectedWorkspace;
        if (_draggedWorkspace is null) return;
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(_draggedWorkspace.Id.ToString());
    }

    private void WorkspacesDragCompleted(UIElement sender, DropCompletedEventArgs e)
    {
        // Fallback for cancelled drags; successful drops restore selection in WorkspacesDrop.
        _draggedWorkspace = null;
        if (_selectionBeforeDrag is not null)
        {
            ViewModel.SelectedWorkspace = _selectionBeforeDrag;
            _selectionBeforeDrag = null;
        }
    }

    private void WorkspacesDragOver(object sender, DragEventArgs e)
    {
        if (_draggedWorkspace is null) return;
        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.IsGlyphVisible = false;
        e.DragUIOverride.Caption = string.Empty;
    }

    private async void WorkspacesDrop(object sender, DragEventArgs e)
    {
        if (_draggedWorkspace is null || sender is not ListView listView) return;
        var item = _draggedWorkspace;
        _draggedWorkspace = null;

        var insertIndex = GetWorkspaceDropIndex(listView, e);
        var oldIndex = ViewModel.Workspaces.IndexOf(item);
        if (oldIndex >= 0)
        {
            insertIndex = Math.Clamp(insertIndex, 0, ViewModel.Workspaces.Count);
            var moveTarget = insertIndex > oldIndex ? insertIndex - 1 : insertIndex;
            if (moveTarget != oldIndex)
            {
                ViewModel.Workspaces.Move(oldIndex, moveTarget);
                await ViewModel.PersistReorderAsync(ViewModel.Workspaces);
            }
        }

        if (_selectionBeforeDrag is not null)
        {
            ViewModel.SelectedWorkspace = _selectionBeforeDrag;
            _selectionBeforeDrag = null;
        }
    }

    private static int GetWorkspaceDropIndex(ListView listView, DragEventArgs e)
    {
        var dropPoint = e.GetPosition(listView);
        for (var i = 0; i < listView.Items.Count; i++)
        {
            if (listView.ContainerFromIndex(i) is not ListViewItem item) continue;
            var itemTop = item.TransformToVisual(listView).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            if (dropPoint.Y < itemTop + item.ActualHeight / 2)
                return i;
        }
        return listView.Items.Count;
    }

    private async void AddWorkspaceButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddWorkspaceDialogAsync();

    private async void CreateWorkspaceCta_Click(object sender, RoutedEventArgs e) =>
        await ShowAddWorkspaceDialogAsync(pickExisting: false);

    private async void OpenWorkspaceCta_Click(object sender, RoutedEventArgs e) =>
        await ShowAddWorkspaceDialogAsync(pickExisting: true);

    private async Task ShowAddWorkspaceDialogAsync(bool? pickExisting = null)
    {
        var dialog = new AddWorkspaceDialog { XamlRoot = Content.XamlRoot };
        if (pickExisting is { } mode)
            dialog.ViewModel.IsPickExisting = mode;
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

    private void ApplyWindowGeometry()
    {
        var saved = LoadWindowGeometry();
        if (saved is not null && IsGeometryOnScreen(saved))
            AppWindow.MoveAndResize(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height));
        else
            ApplyDefaultGeometry();
    }

    private void ApplyDefaultGeometry()
    {
        var wa = DisplayArea.Primary.WorkArea;
        AppWindow.MoveAndResize(new RectInt32(wa.X, wa.Y, wa.Width / 2, wa.Height));
    }

    private static bool IsGeometryOnScreen(WindowGeometry g)
    {
        var displays = DisplayArea.FindAll();
        for (var i = 0; i < displays.Count; i++)
        {
            var wa = displays[i].WorkArea;
            if (g.X < wa.X + wa.Width && g.X + g.Width > wa.X &&
                g.Y < wa.Y + wa.Height && g.Y + g.Height > wa.Y)
                return true;
        }
        return false;
    }

    private void SaveWindowGeometry()
    {
        try
        {
            var pos = AppWindow.Position;
            var size = AppWindow.Size;
            Directory.CreateDirectory(Path.GetDirectoryName(WindowGeometryFilePath)!);
            File.WriteAllText(WindowGeometryFilePath, JsonSerializer.Serialize(
                new WindowGeometry(pos.X, pos.Y, size.Width, size.Height)));
        }
        catch (Exception ex) { Debug.WriteLine($"[Bishop] SaveWindowGeometry: {ex.Message}"); }
    }

    private static WindowGeometry? LoadWindowGeometry()
    {
        if (!File.Exists(WindowGeometryFilePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<WindowGeometry>(File.ReadAllText(WindowGeometryFilePath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Bishop] LoadWindowGeometry: {ex.Message}");
            return null;
        }
    }

    private static string WindowGeometryFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bishop.AI", "window-geometry.json");

    private sealed record WindowGeometry(int X, int Y, int Width, int Height);
}
