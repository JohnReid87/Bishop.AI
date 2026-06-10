using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Workspaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI.Views.Workspaces;

internal sealed class BoardDragDropController
{
    private readonly WorkspaceBoardViewModel _board;
    private readonly ISafeAsyncRunner _safeAsync;
    private readonly ListView _lanesListView;

    private CardViewModel? _draggedCard;
    private LaneViewModel? _dragSourceLane;
    private LaneViewModel? _currentDropTargetLane;
    private DispatcherTimer? _autoScrollTimer;
    private ScrollViewer? _autoScrollTarget;
    private double _autoScrollVelocity;

    public BoardDragDropController(WorkspaceBoardViewModel board, ISafeAsyncRunner safeAsync, ListView lanesListView)
    {
        _board = board;
        _safeAsync = safeAsync;
        _lanesListView = lanesListView;
    }

    public void OnDragStarting(UIElement sender, DragStartingEventArgs e)
    {
        _draggedCard = GetCardFromSender(sender);
        if (_draggedCard is null) return;
        _dragSourceLane = _board.Lanes.FirstOrDefault(l =>
            string.Equals(l.Name, _draggedCard.LaneName, StringComparison.OrdinalIgnoreCase));
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(_draggedCard.Id.ToString());
        _lanesListView.CanReorderItems = false;
    }

    public void OnDropCompleted()
    {
        ClearAllDropTargets();
        StopAutoScroll();
        _lanesListView.CanReorderItems = true;
    }

    public void OnDragOver(object sender, DragEventArgs e)
    {
        if (_draggedCard is null) return;
        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.IsGlyphVisible = false;
        e.DragUIOverride.Caption = string.Empty;
        if ((sender as FrameworkElement)?.DataContext is LaneViewModel lane && lane != _currentDropTargetLane)
        {
            ClearAllDropTargets();
            lane.IsDropTarget = true;
            _currentDropTargetLane = lane;
        }

        var scrollViewer = FindVisualChild<ScrollViewer>(sender as DependencyObject);
        if (scrollViewer is null) return;

        var pos = e.GetPosition(scrollViewer);
        var velocity = DragDropComputer.ComputeScrollVelocity(pos.Y, scrollViewer.ViewportHeight);
        if (velocity != 0)
        {
            _autoScrollTarget = scrollViewer;
            _autoScrollVelocity = velocity;
            if (_autoScrollTimer is null)
            {
                _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _autoScrollTimer.Tick += AutoScrollTimer_Tick;
            }
            if (!_autoScrollTimer.IsEnabled)
                _autoScrollTimer.Start();
        }
        else
        {
            StopAutoScroll();
        }
    }

    public void OnDragLeave()
    {
        ClearAllDropTargets();
        StopAutoScroll();
    }

    public void OnDrop(object sender, DragEventArgs e) =>
        _ = _safeAsync.RunAsync(async () =>
        {
            if (_draggedCard is null || _dragSourceLane is null) return;
            if ((sender as FrameworkElement)?.DataContext is not LaneViewModel targetLane) return;

            ClearAllDropTargets();
            StopAutoScroll();

            var position = GetDropIndex(FindVisualChild<ItemsRepeater>(sender as DependencyObject), e, targetLane);
            var card = _draggedCard;
            var targetLaneName = targetLane.Name;

            _draggedCard = null;
            _dragSourceLane = null;

            await _board.MoveCardAsync(card.Id, targetLaneName, position);
            await _board.RefreshCommand.ExecuteAsync(null);
        });

    private void AutoScrollTimer_Tick(object? sender, object e)
    {
        if (_autoScrollTarget is null) { StopAutoScroll(); return; }
        var newOffset = _autoScrollTarget.VerticalOffset + _autoScrollVelocity;
        _autoScrollTarget.ChangeView(null, newOffset, null, true);
    }

    private void StopAutoScroll()
    {
        _autoScrollTimer?.Stop();
        _autoScrollTarget = null;
        _autoScrollVelocity = 0;
    }

    private void ClearAllDropTargets()
    {
        if (_currentDropTargetLane is not null)
        {
            _currentDropTargetLane.IsDropTarget = false;
            _currentDropTargetLane = null;
        }
    }

    private static int GetDropIndex(ItemsRepeater? repeater, DragEventArgs e, LaneViewModel targetLane)
    {
        if (repeater is null) return targetLane.FilteredCards.Count + 1;

        var dropPoint = e.GetPosition(repeater);
        var cardOffset = 0;
        for (var i = 0; i < targetLane.LaneItems.Count; i++)
        {
            var cardsInItem = targetLane.LaneItems[i] is BatchGroupViewModel bg ? bg.Cards.Count : 1;
            if (repeater.TryGetElement(i) is not FrameworkElement item)
            {
                cardOffset += cardsInItem;
                continue;
            }
            var itemTop = item.TransformToVisual(repeater).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            if (dropPoint.Y < itemTop + item.ActualHeight / 2)
                return cardOffset + 1;
            cardOffset += cardsInItem;
        }
        return targetLane.FilteredCards.Count + 1;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }

    private static CardViewModel? GetCardFromSender(object sender)
    {
        var element = sender as DependencyObject;
        while (element is not null)
        {
            if (element is FrameworkElement { Tag: CardViewModel card })
                return card;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
