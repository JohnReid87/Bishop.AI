using Bishop.App.Cards.MoveCard;
using Bishop.App.Lanes.AddLane;
using Bishop.App.Lanes.MoveLane;
using Bishop.App.Lanes.RemoveLane;
using Bishop.App.Lanes.RenameLane;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.UI.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI.Views;

public sealed partial class WorkspaceDetailPage : Page
{
    private WorkspaceItemViewModel? _item;
    private CardViewModel? _draggedCard;
    private LaneViewModel? _dragSourceLane;

    public WorkspaceBoardViewModel Board { get; }

    public WorkspaceDetailPage()
    {
        Board = App.Services.GetRequiredService<WorkspaceBoardViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;

        if (e.Parameter is WorkspaceItemViewModel vm)
        {
            _item = vm;
            _item.PropertyChanged += OnItemPropertyChanged;
            UpdateView(vm);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceItemViewModel.IsPathMissing))
            UpdatePathStatus();
    }

    private void UpdateView(WorkspaceItemViewModel vm)
    {
        WorkspaceNameText.Text = vm.Name;
        WorkspacePathText.Text = vm.Path;
        UpdatePathStatus();
        _ = Board.LoadAsync(vm.Id);
    }

    private void UpdatePathStatus()
    {
        var missing = _item?.IsPathMissing ?? false;
        LaunchButton.IsEnabled = !missing;
        PathWarningBar.IsOpen = missing;
        ToolTipService.SetToolTip(LaunchButtonWrapper, missing ? "The workspace directory is missing." : null);
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        var mediator = App.Services.GetRequiredService<IMediator>();
        var launchedWithTerminal = await mediator.Send(new LaunchWorkspaceCommand(_item.Path));
        FallbackWarningBar.IsOpen = !launchedWithTerminal;
    }

    private async void ViewCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardViewModel card)
            return;

        var dialog = new CardDetailDialog(card) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
        if (dialog.ViewModel.Deleted)
            await Board.RefreshCommand.ExecuteAsync(null);
    }

    private void Card_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        _draggedCard = (sender as FrameworkElement)?.DataContext as CardViewModel;
        if (_draggedCard is null) return;
        _dragSourceLane = Board.Lanes.FirstOrDefault(l => l.Id == _draggedCard.LaneId);
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(_draggedCard.Id.ToString());
    }

    private void Cards_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedCard is null) return;
        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.IsGlyphVisible = false;
        e.DragUIOverride.Caption = string.Empty;
        if ((sender as FrameworkElement)?.DataContext is LaneViewModel lane)
            lane.IsDropTarget = true;
    }

    private void Cards_DragLeave(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LaneViewModel lane)
            lane.IsDropTarget = false;
    }

    private async void Cards_Drop(object sender, DragEventArgs e)
    {
        if (_draggedCard is null || _dragSourceLane is null) return;
        var targetLane = (sender as FrameworkElement)?.DataContext as LaneViewModel;
        if (targetLane is null) return;

        var position = GetDropIndex(sender as ListView, e, targetLane);
        var card = _draggedCard;
        var targetLaneId = targetLane.Id;

        _draggedCard = null;
        _dragSourceLane = null;

        var mediator = App.Services.GetRequiredService<IMediator>();
        await mediator.Send(new MoveCardCommand(card.Id, targetLaneId, position));
        await Board.RefreshCommand.ExecuteAsync(null);
    }

    private static int GetDropIndex(ListView? listView, DragEventArgs e, LaneViewModel targetLane)
    {
        if (listView is null) return targetLane.Cards.Count + 1;

        var dropPoint = e.GetPosition(listView);
        for (var i = 0; i < listView.Items.Count; i++)
        {
            if (listView.ContainerFromIndex(i) is not ListViewItem item) continue;
            var itemTop = item.TransformToVisual(listView).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            if (dropPoint.Y < itemTop + item.ActualHeight / 2)
                return i + 1;
        }
        return listView.Items.Count + 1;
    }

    private async void AddLane_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        var dialog = new AddLaneDialog { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var mediator = App.Services.GetRequiredService<IMediator>();
        await mediator.Send(new AddLaneCommand(_item.Id, dialog.ViewModel.Name));
        await Board.RefreshCommand.ExecuteAsync(null);
    }

    private void LaneHeader_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }

    private async void RenameLane_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LaneViewModel lane) return;

        var nameBox = new TextBox { Text = lane.Name, SelectionStart = lane.Name.Length };
        var renameDialog = new ContentDialog
        {
            Title = "Rename Lane",
            Content = nameBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        renameDialog.Resources["ContentDialogBackground"] = Application.Current.Resources["AppSurfaceBrush"];

        if (await renameDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var mediator = App.Services.GetRequiredService<IMediator>();
            await mediator.Send(new RenameLaneCommand(lane.Id, nameBox.Text));
            await Board.RefreshCommand.ExecuteAsync(null);
        }
    }

    private async void DeleteLane_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LaneViewModel lane) return;

        var mediator = App.Services.GetRequiredService<IMediator>();
        try
        {
            await mediator.Send(new RemoveLaneCommand(lane.Id));
            await Board.RefreshCommand.ExecuteAsync(null);
        }
        catch (InvalidOperationException ex)
        {
            LaneErrorBar.Title = "Cannot delete lane";
            LaneErrorBar.Message = ex.Message;
            LaneErrorBar.IsOpen = true;
        }
    }

    private async void Lanes_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        var mediator = App.Services.GetRequiredService<IMediator>();
        var ordered = LanesListView.Items.OfType<LaneViewModel>().ToList();
        for (var i = 0; i < ordered.Count; i++)
            await mediator.Send(new MoveLaneCommand(ordered[i].Id, i + 1));
        await Board.RefreshCommand.ExecuteAsync(null);
    }
}
