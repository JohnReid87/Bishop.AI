using Bishop.Core;
using CommunityToolkit.WinUI.Controls;
using Bishop.UI.Services;
using Bishop.UI.Views.Controls;
using Bishop.UI.Views.Skills;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Bishop.UI.Views.Workspaces;

public sealed partial class WorkspaceDetailPage : Page
{
    private readonly DbChangeWatcher _dbWatcher;
    private readonly IDialogService _dialogService;
    private readonly ILogger<WorkspaceDetailPage> _logger;
    private readonly ISafeAsyncRunner _safeAsync;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly SkillLauncher _skillLauncher;
    private WorkspaceItemViewModel? _item;
    private BoardDragDropController? _dragDrop;
    private NotesSplitterDragHandler? _notesSplitter;
    private Flyout? _commitsFlyout;

    public WorkspaceBoardViewModel Board { get; }
    public WorkspaceNotesViewModel Notes { get; }
    public WorkspaceMonitoringViewModel Monitoring { get; }
    public WorkspaceBatchesViewModel Batches { get; }
    public CommitsFlyoutViewModel Commits { get; }

    public WorkspaceDetailPage()
    {
        Board = App.Services.GetRequiredService<WorkspaceBoardViewModel>();
        Notes = App.Services.GetRequiredService<WorkspaceNotesViewModel>();
        Monitoring = App.Services.GetRequiredService<WorkspaceMonitoringViewModel>();
        Batches = App.Services.GetRequiredService<WorkspaceBatchesViewModel>();
        Commits = App.Services.GetRequiredService<CommitsFlyoutViewModel>();
        _dbWatcher = App.Services.GetRequiredService<DbChangeWatcher>();
        _dialogService = App.Services.GetRequiredService<IDialogService>();
        _logger = App.Services.GetRequiredService<ILogger<WorkspaceDetailPage>>();
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        _uiDispatcher = App.Services.GetRequiredService<IUiDispatcher>();
        _skillLauncher = new SkillLauncher(Board);
        InitializeComponent();
        _dragDrop = new BoardDragDropController(Board, _safeAsync, LanesListView);
        _notesSplitter = new NotesSplitterDragHandler(Notes, this);
        _commitsFlyout = new Flyout
        {
            Content = new CommitsFlyoutControl(Commits),
            Placement = FlyoutPlacementMode.Bottom,
        };
        Commits.CommitActivated += row => _safeAsync.RunAsync(async () =>
        {
            _commitsFlyout.Hide();
            var pkg = new DataPackage();
            pkg.SetText(row.FullHash);
            Clipboard.SetContent(pkg);
            await ShowCopiedToastAsync();
        });
        Board.StagingTray.Cards.CollectionChanged += OnStagingTrayCardsChanged;
        Monitoring.ViewFindingsRequested += OnViewFindingsRequested;
        Monitoring.ViewReportRequested += OnViewReportRequested;
    }

    private void OnViewFindingsRequested(Bishop.ViewModels.Findings.FindingsPageNavArgs args)
        => Frame?.Navigate(typeof(Bishop.UI.Views.Findings.FindingsPage),
            args with { Workspace = _item, SourceTab = Bishop.ViewModels.Workspaces.WorkspaceTab.Monitoring });

    private void OnViewReportRequested(Uri uri)
        => _ = App.ReportViewer!.ShowReport(uri);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _dbWatcher.DatabaseChanged += OnDatabaseChanged;
        if (App.MainWindow is not null)
            App.MainWindow.Activated += OnWindowActivated;

        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;

        if (e.Parameter is WorkspaceDetailPageNavArgs navArgs)
        {
            _item = navArgs.Workspace;
            _item.PropertyChanged += OnItemPropertyChanged;
            _ = _safeAsync.RunAsync(() => UpdateViewAsync(navArgs.Workspace));
            if (navArgs.InitialTab.HasValue)
                MainTabView.SelectedIndex = (int)navArgs.InitialTab.Value;
        }
        else if (e.Parameter is WorkspaceItemViewModel vm)
        {
            _item = vm;
            _item.PropertyChanged += OnItemPropertyChanged;
            _ = _safeAsync.RunAsync(() => UpdateViewAsync(vm));
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _dbWatcher.DatabaseChanged -= OnDatabaseChanged;
        if (App.MainWindow is not null)
            App.MainWindow.Activated -= OnWindowActivated;
        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;
        // Notes is Transient + IDisposable; flush pending edits then release the FileSystemWatcher.
        _ = _safeAsync.RunAsync(Notes.FlushAsync);
        Notes.Dispose();
    }

    private void OnDatabaseChanged(object? sender, EventArgs e) => RefreshAllOnUiThread();

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated) return;
        RefreshAllOnUiThread();
    }

    private void RefreshAllOnUiThread() =>
        _uiDispatcher.TryEnqueue(() => _safeAsync.RunAsync(() => Task.WhenAll(
            Board.RefreshCommand.ExecuteAsync(null),
            Monitoring.RefreshCommand.ExecuteAsync(null),
            Batches.RefreshCommand.ExecuteAsync(null))));

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceItemViewModel.IsPathMissing))
            UpdatePathStatus();
    }

    private async Task UpdateViewAsync(WorkspaceItemViewModel vm)
    {
        WorkspaceNameText.Text = vm.Name;
        WorkspacePathText.Text = vm.Path;
        ToolTipService.SetToolTip(WorkspacePathText, vm.Path);
        Board.WorkspacePath = vm.Path;
        UpdatePathStatus();
        await LoadSkillsAsync();
        _ = _safeAsync.RunAsync(() => Board.LoadAsync(vm.Id));
        _ = _safeAsync.RunAsync(() => Notes.LoadAsync(vm.Id, vm.Path));
        _ = _safeAsync.RunAsync(() => Monitoring.LoadAsync(vm.Id, vm.Path));
        _ = _safeAsync.RunAsync(() => Batches.LoadAsync(vm.Id, vm.Path));
    }

    private async Task LoadSkillsAsync()
    {
        await Board.LoadSkillsAsync();
        WorkspaceSkillsButton.Visibility = Board.WorkspaceSkills.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePathStatus()
    {
        var missing = _item?.IsPathMissing ?? false;
        CommitsButton.IsEnabled = !missing;
        TerminalButton.IsEnabled = !missing;
        ClaudeButton.IsEnabled = !missing;
        PathWarningBar.IsOpen = missing;
        ToolTipService.SetToolTip(LaunchButtonWrapper, missing ? "The workspace directory is missing." : null);
        UpdateNotificationPanel();
    }

    private void UpdateNotificationPanel() =>
        NotificationPanel.Visibility =
            PathWarningBar.IsOpen || FallbackWarningBar.IsOpen || CopiedBar.IsOpen
                ? Visibility.Visible
                : Visibility.Collapsed;

    private void InfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => UpdateNotificationPanel();

    private async void ClaudeButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var launchedWithTerminal = await Board.LaunchClaudeAsync(_item.Path, SnapHelper.ComputeSnap());
            FallbackWarningBar.IsOpen = !launchedWithTerminal;
            UpdateNotificationPanel();
        });

    private async void TerminalButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            await Board.LaunchTerminalAsync(_item.Path, SnapHelper.ComputeSnap());
        });

    private async void WorkspaceSkillsButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null || Board.WorkspaceSkills.Length == 0) return;

            var items = await Board.BuildWorkspaceSkillLaunchItemsAsync();
            ShowSkillFlyout((FrameworkElement)sender, items);
        });

    private void ShowSkillFlyout(FrameworkElement anchor, IReadOnlyList<SkillLaunchItem> items) =>
        SkillFlyoutFactory.Show(anchor, items,
            onLaunch: async (captured, chosenModel) =>
            {
                await Board.SetSkillModelAsync(captured.Name, chosenModel);
                await _skillLauncher.LaunchAsync(captured, chosenModel, XamlRoot);
            },
            onView: captured => App.MarkdownViewer!.ShowContent(captured.Name, captured.MarkdownBody));

    private async void CommitsButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            await Commits.LoadAsync(_item.Path);
            _commitsFlyout!.ShowAt((FrameworkElement)sender);
        });

    private async Task ShowCopiedToastAsync()
    {
        CopiedBar.IsOpen = true;
        UpdateNotificationPanel();
        await Task.Delay(2000);
        CopiedBar.IsOpen = false;
        UpdateNotificationPanel();
    }



    private async void CardTitle_Tapped(object sender, TappedRoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;

            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            if (ctrl.HasFlag(CoreVirtualKeyStates.Down))
            {
                Board.ToggleCardSelection(card);
                return;
            }

            var vm = await _dialogService.ShowCardDetailDialogAsync(card, Board.CardSkills, _item?.Path ?? string.Empty, _item?.Id ?? Guid.Empty, XamlRoot);
            if (vm.Deleted || vm.Updated)
                await Board.RefreshCommand.ExecuteAsync(null);
        });

    private void OnStagingTrayCardsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (Board.StagingTray.Cards.Count > 0 && string.IsNullOrEmpty(Board.StagingTray.BaseBranch) && _item is not null)
            _ = _safeAsync.RunAsync(async () =>
            {
                var branch = await Board.GetCurrentBranchAsync(_item.Path);
                Board.StagingTray.BaseBranch = branch;
            });
    }

    private void StagingTrayCancel_Click(object sender, RoutedEventArgs e)
        => Board.ClearSelection();

    private async void StagingTrayCreate_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var tray = Board.StagingTray;
            var cardNumbers = tray.Cards.Select(c => c.Number).ToArray();
            var created = await Batches.CreateFromTrayAsync(
                _item.Id, _item.Path, tray.Name, tray.Branch, tray.Model, cardNumbers);
            if (!created) return;

            Board.ClearSelection();
            await Board.RefreshCommand.ExecuteAsync(null);
            await Batches.RefreshCommand.ExecuteAsync(null);
        });

    private void StagingTrayRemoveChip_Click(object sender, RoutedEventArgs e)
    {
        if (GetCardFromSender(sender) is CardViewModel card)
            Board.ToggleCardSelection(card);
    }

    private async void CardSkillsButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null || Board.CardSkills.Length == 0) return;
            if (GetCardFromSender(sender) is not CardViewModel card) return;

            var items = await Board.BuildCardSkillLaunchItemsAsync(card);
            ShowSkillFlyout((FrameworkElement)sender, items);
        });

    private async void CardCloseButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;

            await Board.ToggleCardClosedAsync(card.Id, card.IsClosed);

            card.IsClosed = !card.IsClosed;
            Board.RefreshLaneItems();
        });

    private async void CardTagChip_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;
            await OpenCardTagPickerAsync((FrameworkElement)sender, card, card.TagName);
        });

    private async void CardAddTagButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;
            await OpenCardTagPickerAsync((FrameworkElement)sender, card, null);
        });

    private async Task OpenCardTagPickerAsync(FrameworkElement anchor, CardViewModel card, string? currentlySelected)
    {
        if (_item is null) return;

        IReadOnlyList<Bishop.Core.TagInfo> allTags;
        try
        {
            allTags = await Board.GetTagsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load tags for card tag picker");
            return;
        }

        var flyout = TagPickerFlyout.Build(allTags, [], async (name, _) =>
        {
            await Board.UpdateCardTagAsync(card.Id, name);
            await Board.RefreshCommand.ExecuteAsync(null);
        }, currentlySelected);
        flyout.ShowAt(anchor);
    }

    private async void RunNowSkillButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if ((sender as FrameworkElement)?.DataContext is not SkillRunRowViewModel row) return;
            var item = await Board.BuildWorkspaceSkillLaunchItemAsync(row.SkillName);
            if (item is null) return;
            await _skillLauncher.LaunchAsync(item, row.SelectedModelId, XamlRoot);
        });

    private static BatchItemViewModel? GetBatchFromSender(object sender) =>
        (sender as FrameworkElement)?.DataContext as BatchItemViewModel;

    private void CompactCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (GetCardFromSender(sender) is not CardViewModel card) return;
        if (!card.IsAutoRunFailedIndicatorVisible || card.LaneName != SystemLaneNames.Doing) return;
        var batch = card.BatchId.HasValue
            ? Batches.Batches.FirstOrDefault(b => b.Id == card.BatchId.Value)
            : null;
        if (batch is null || !batch.CanResume) return;

        var markDoneItem = new MenuFlyoutItem { Text = "Mark Done & resume batch" };
        markDoneItem.Click += (_, _) => _ = _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            await Batches.MarkCardDoneAndResumeAsync(card.Id, batch.Name, _item.Path, batch.Model, SnapHelper.ComputeSnap());
            await Task.WhenAll(Board.RefreshCommand.ExecuteAsync(null), Batches.RefreshCommand.ExecuteAsync(null));
        });

        var removeItem = new MenuFlyoutItem { Text = "Remove from batch & resume" };
        removeItem.Click += (_, _) => _ = _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            await Batches.RemoveCardAndResumeAsync(batch.Name, card.Id, _item.Path, batch.Model, SnapHelper.ComputeSnap());
            await Task.WhenAll(Board.RefreshCommand.ExecuteAsync(null), Batches.RefreshCommand.ExecuteAsync(null));
        });

        var flyout = new MenuFlyout();
        flyout.Items.Add(markDoneItem);
        flyout.Items.Add(removeItem);
        flyout.ShowAt((FrameworkElement)sender);
    }

    private async void BatchRun_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            await Batches.LaunchBatch(_item.Path, batch.Name, batch.Model, SnapHelper.ComputeSnap());
        });

    private async void BatchResume_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            await Batches.ResumeBatch(_item.Path, batch.Name, batch.Model, SnapHelper.ComputeSnap());
        });

    private async void BatchPause_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            await Batches.RequestStopAsync(batch.Id);
        });

    private async void BatchMerge_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            BatchMergeOutcome? result = null;
            try
            {
                result = await Batches.MergeAsync(batch.Name, _item.Path);
            }
            finally
            {
                await Board.RefreshCommand.ExecuteAsync(null);
                await Batches.RefreshCommand.ExecuteAsync(null);
            }
            if (result is { Success: false })
                BatchMergeFailureFlyout.Show((FrameworkElement)sender, result);
        });

    private async void BatchCleanUp_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            try
            {
                await Batches.CleanUpAsync(batch.Name, _item.Path);
            }
            finally
            {
                await Board.RefreshCommand.ExecuteAsync(null);
                await Batches.RefreshCommand.ExecuteAsync(null);
            }
        });

    private async void BatchAbandon_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            try
            {
                await Batches.AbandonAsync(batch.Name, _item.Path);
            }
            finally
            {
                await Board.RefreshCommand.ExecuteAsync(null);
                await Batches.RefreshCommand.ExecuteAsync(null);
            }
        });

    private async void BatchRemove_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            try
            {
                await Batches.RemoveAsync(batch.Name);
            }
            finally
            {
                await Batches.RefreshCommand.ExecuteAsync(null);
            }
        });

    private async void ClearClosedBatches_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var closed = Batches.Batches.Where(b => b.CanRemove).ToList();
            if (closed.Count == 0) return;
            var message = $"Remove {closed.Count} closed {(closed.Count == 1 ? "batch" : "batches")}? Cards stay on the board.";
            if (!await ConfirmFlyout.ShowAsync((FrameworkElement)sender, message, "Remove")) return;
            await Batches.RemoveAllClosedAsync(closed);
            await Batches.RefreshCommand.ExecuteAsync(null);
        });

    private void BatchNameTextBlock_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BatchItemViewModel batch) return;
        e.Handled = true;
        var root = (sender as FrameworkElement)?.Parent as FrameworkElement;
        if (root?.FindName("BatchNameTextBox") is not TextBox textBox) return;
        batch.IsNameEditing = true;
        textBox.Text = batch.Name;
        textBox.Focus(FocusState.Programmatic);
        textBox.SelectAll();
    }

    private async void BatchNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not BatchItemViewModel batch) return;
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await Batches.CommitBatchNameAsync(batch, textBox.Text);
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                batch.IsNameEditing = false;
            }
        });

    private async void BatchNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not BatchItemViewModel batch) return;
            if (batch.IsNameEditing)
                await Batches.CommitBatchNameAsync(batch, textBox.Text);
        });

    private void CardRoot_Loaded(object sender, RoutedEventArgs e)
        => Animations.EntranceAnimation.ApplyCardEntrance(sender as FrameworkElement);

    private void Card_DragStarting(UIElement sender, DragStartingEventArgs e) => _dragDrop!.OnDragStarting(sender, e);
    private void Card_DropCompleted(UIElement sender, DropCompletedEventArgs e) => _dragDrop!.OnDropCompleted();
    private void Cards_DragOver(object sender, DragEventArgs e) => _dragDrop!.OnDragOver(sender, e);
    private void Cards_DragLeave(object sender, DragEventArgs e) => _dragDrop!.OnDragLeave();
    private void Cards_Drop(object sender, DragEventArgs e) => _dragDrop!.OnDrop(sender, e);

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

    private void NotesSplitter_PointerPressed(object sender, PointerRoutedEventArgs e) => _notesSplitter!.OnPointerPressed(sender, e);
    private void NotesSplitter_PointerMoved(object sender, PointerRoutedEventArgs e) => _notesSplitter!.OnPointerMoved(sender, e);
    private void NotesSplitter_PointerReleased(object sender, PointerRoutedEventArgs e) => _notesSplitter!.OnPointerReleased(sender, e);
    private void NotesSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => _notesSplitter!.OnPointerCaptureLost(sender, e);

    private void BeginAddCard_Click(object sender, RoutedEventArgs e)
    {
        // button lives in a StackPanel inside the header Grid (col 1) → lane Grid → cards ListView → Header
        if (sender is not FrameworkElement fe) return;
        var headerGrid = (fe.Parent as StackPanel)?.Parent as Grid ?? fe.Parent as Grid;
        var laneGrid = headerGrid?.Parent as Grid;
        var listView = laneGrid?.Children.OfType<ListView>().FirstOrDefault();
        var headerPanel = listView?.Header as StackPanel;
        var textBox = headerPanel?.Children.OfType<TextBox>().FirstOrDefault();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => textBox?.Focus(FocusState.Programmatic));
    }

    private void CardSearch_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            Board.SearchText = string.Empty;
        }
    }

    private void AddCardTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LaneViewModel lane) return;
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = _safeAsync.RunAsync(() => lane.ConfirmAddCardCommand.ExecuteAsync(null));
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            lane.CancelAddCardCommand.Execute(null);
        }
    }

    private async void NotesTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (e.Key != VirtualKey.S) return;
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            if (!ctrl.HasFlag(CoreVirtualKeyStates.Down)) return;
            e.Handled = true;
            await Notes.QuickSaveAsync();
        });
}
