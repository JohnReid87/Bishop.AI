using Bishop.App.Cards.MoveCard;
using Bishop.App.Lanes.AddLane;
using Bishop.UI.Services;
using Bishop.App.Settings;
using Bishop.App.Lanes.MoveLane;
using Bishop.App.Lanes.RemoveLane;
using Bishop.App.Lanes.RenameLane;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Terminal;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.Core.Skills;
using Bishop.UI.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Bishop.UI.Views;

public sealed partial class WorkspaceDetailPage : Page
{
    private readonly DbChangeWatcher _dbWatcher;
    private WorkspaceItemViewModel? _item;
    private CardViewModel? _draggedCard;
    private LaneViewModel? _dragSourceLane;
    private bool _isDraggingNotes;
    private double _dragStartPageY;
    private double _dragStartNoteHeight;
    private IReadOnlyList<InstalledSkill> _cardSkills = [];
    private IReadOnlyList<InstalledSkill> _workspaceSkills = [];

    private static readonly (string Id, string Label)[] Models =
    [
        ("claude-opus-4-7",           "Opus 4.7"),
        ("claude-sonnet-4-6",         "Sonnet 4.6"),
        ("claude-haiku-4-5-20251001", "Haiku 4.5"),
    ];
    private const string DefaultModel = "claude-sonnet-4-6";

    public WorkspaceBoardViewModel Board { get; }
    public WorkspaceNotesViewModel Notes { get; }

    public WorkspaceDetailPage()
    {
        Board = App.Services.GetRequiredService<WorkspaceBoardViewModel>();
        Notes = App.Services.GetRequiredService<WorkspaceNotesViewModel>();
        _dbWatcher = App.Services.GetRequiredService<DbChangeWatcher>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _dbWatcher.DatabaseChanged += OnDatabaseChanged;

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
        _dbWatcher.DatabaseChanged -= OnDatabaseChanged;
        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;
        _ = Notes.FlushAsync();
    }

    private void OnDatabaseChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
            await Board.RefreshCommand.ExecuteAsync(null));
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceItemViewModel.IsPathMissing))
            UpdatePathStatus();
    }

    private async void UpdateView(WorkspaceItemViewModel vm)
    {
        WorkspaceNameText.Text = vm.Name;
        WorkspacePathText.Text = vm.Path;
        UpdatePathStatus();
        await LoadSkillsAsync();
        _ = Board.LoadAsync(vm.Id);
        _ = Notes.LoadAsync(vm.Id, vm.Path);
    }

    private async Task LoadSkillsAsync()
    {
        var mediator = App.Services.GetRequiredService<IMediator>();
        var skills = await mediator.Send(new DiscoverSkillsQuery());
        _cardSkills = skills.Where(s => s.Scope == "card" && s.Command is not null).ToList();
        _workspaceSkills = skills.Where(s => s.Scope == "workspace" && s.Command is not null).ToList();
        CardViewModel.CardSkillsButtonVisibility = _cardSkills.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceSkillsButton.Visibility = _workspaceSkills.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePathStatus()
    {
        var missing = _item?.IsPathMissing ?? false;
        TerminalButton.IsEnabled = !missing;
        ClaudeButton.IsEnabled = !missing;
        PathWarningBar.IsOpen = missing;
        ToolTipService.SetToolTip(LaunchButtonWrapper, missing ? "The workspace directory is missing." : null);
    }

    private async void ClaudeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        var mediator = App.Services.GetRequiredService<IMediator>();
        var launchedWithTerminal = await mediator.Send(new LaunchWorkspaceCommand(_item.Path, ComputeSnap()));
        FallbackWarningBar.IsOpen = !launchedWithTerminal;
    }

    private async void TerminalButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        var mediator = App.Services.GetRequiredService<IMediator>();
        await mediator.Send(new LaunchPlainTerminalCommand(_item.Path, ComputeSnap()));
    }

    private async void WorkspaceSkillsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null || _workspaceSkills.Count == 0) return;
        var appSettings = App.Services.GetRequiredService<IAppSettings>();

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

        foreach (var (skill, i) in _workspaceSkills.Select((s, i) => (s, i)))
        {
            var rendered = RenderCommand(skill.Command!, null, _item.Path);
            var workspacePath = _item.Path;
            var capturedSkill = skill;
            var settingKey = $"skill.{skill.Name}.last_model";
            var savedModel = await appSettings.GetAsync(settingKey) ?? DefaultModel;

            panel.Children.Add(MakeSkillRow(skill.Name, savedModel, async chosenModel =>
            {
                await appSettings.SetAsync(settingKey, chosenModel);
                flyout.Hide();
                await LaunchSkillAsync(capturedSkill, rendered, workspacePath, chosenModel);
            }));
            if (i < _workspaceSkills.Count - 1)
                panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        }

        flyout.Content = panel;
        flyout.ShowAt((FrameworkElement)sender);
    }

    private async void CardSkillsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null || _cardSkills.Count == 0) return;
        if ((sender as FrameworkElement)?.DataContext is not CardViewModel card) return;
        var appSettings = App.Services.GetRequiredService<IAppSettings>();

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

        foreach (var (skill, i) in _cardSkills.Select((s, i) => (s, i)))
        {
            var rendered = RenderCommand(skill.Command!, card, _item.Path);
            var workspacePath = _item.Path;
            var capturedSkill = skill;
            var settingKey = $"skill.{skill.Name}.last_model";
            var savedModel = await appSettings.GetAsync(settingKey) ?? DefaultModel;

            panel.Children.Add(MakeSkillRow(skill.Name, savedModel, async chosenModel =>
            {
                await appSettings.SetAsync(settingKey, chosenModel);
                flyout.Hide();
                await LaunchSkillAsync(capturedSkill, rendered, workspacePath, chosenModel);
            }));
            if (i < _cardSkills.Count - 1)
                panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        }

        flyout.Content = panel;
        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task LaunchSkillAsync(InstalledSkill skill, string rendered, string workspacePath, string? modelId = null)
    {
        if (skill.Stage)
        {
            var dialog = new SkillStageDialog(skill.Name, skill.StagePrompt) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var input = dialog.InputText?.Trim() ?? string.Empty;
            if (input.Length > 0)
                rendered = $"{rendered} {input}";
        }

        var mediator = App.Services.GetRequiredService<IMediator>();
        await mediator.Send(new LaunchSkillCommand(workspacePath, rendered, ComputeSnap(), modelId));
    }

    private static FrameworkElement MakeSkillRow(string skillName, string selectedModelId, Func<string, Task> onLaunch)
    {
        var currentModelId = selectedModelId;
        var currentLabel = Models.FirstOrDefault(m => m.Id == selectedModelId).Label ?? "Sonnet 4.6";

        var nameText = new TextBlock
        {
            Text = skillName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120,
            MinWidth = 60,
            FontSize = 12,
        };

        var modelBtn = new Button
        {
            Content = $"{currentLabel} ▾",
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
            FontSize = 12,
        };

        var modelFlyout = new MenuFlyout();
        foreach (var (id, label) in Models)
        {
            var capturedId = id;
            var capturedLabel = label;
            var mi = new MenuFlyoutItem { Text = label };
            mi.Click += (_, _) =>
            {
                currentModelId = capturedId;
                modelBtn.Content = $"{capturedLabel} ▾";
            };
            modelFlyout.Items.Add(mi);
        }
        modelBtn.Flyout = modelFlyout;

        var launchBtn = new Button
        {
            Content = "▶",
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        };
        launchBtn.Click += async (_, _) => await onLaunch(currentModelId);

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(nameText);
        row.Children.Add(modelBtn);
        row.Children.Add(launchBtn);
        return row;
    }

    private static TerminalSnap ComputeSnap()
    {
        var display = DisplayArea.GetFromWindowId(App.MainWindow!.AppWindow.Id, DisplayAreaFallback.Primary);
        var wa = display.WorkArea;
        return TerminalSnap.RightHalf(wa.X, wa.Y, wa.Width, wa.Height);
    }

    private static string RenderCommand(string template, CardViewModel? card, string workspacePath) =>
        template
            .Replace("{{workspace_path}}", workspacePath)
            .Replace("{{card_number}}", card?.Number.ToString() ?? string.Empty);

    private async void ViewCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardViewModel card)
            return;

        var dialog = new CardDetailDialog(card, _cardSkills, _item?.Path ?? string.Empty, _item?.Id ?? Guid.Empty, _item?.GitHubRepo) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
        if (dialog.ViewModel.Deleted || dialog.ViewModel.Updated)
            await Board.RefreshCommand.ExecuteAsync(null);
    }

    private async void WorkspaceSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;

        var repoBox = new TextBox
        {
            PlaceholderText = "owner/repo  (clear to unlink)",
            Text = _item.GitHubRepo ?? string.Empty,
            Width = 300,
        };

        var dialog = new ContentDialog
        {
            Title = "Workspace Settings",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "GitHub repository" },
                    repoBox,
                }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var mediator = App.Services.GetRequiredService<IMediator>();
        var repo = repoBox.Text.Trim();
        if (string.IsNullOrEmpty(repo))
        {
            await mediator.Send(new UnsetWorkspaceGitHubRepoCommand(_item.Id));
            _item.GitHubRepo = null;
        }
        else
        {
            await mediator.Send(new SetWorkspaceGitHubRepoCommand(_item.Id, repo));
            _item.GitHubRepo = repo;
        }
    }

    private void Card_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        _draggedCard = (sender as FrameworkElement)?.DataContext as CardViewModel;
        if (_draggedCard is null) return;
        _dragSourceLane = Board.Lanes.FirstOrDefault(l => l.Id == _draggedCard.LaneId);
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(_draggedCard.Id.ToString());
        LanesListView.CanReorderItems = false;
    }

    private void Card_DropCompleted(UIElement sender, DropCompletedEventArgs e)
    {
        LanesListView.CanReorderItems = true;
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

    private void NotesSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingNotes = true;
        _dragStartPageY = e.GetCurrentPoint(this).Position.Y;
        _dragStartNoteHeight = Notes.PanelHeight;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void NotesSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingNotes) return;
        var delta = _dragStartPageY - e.GetCurrentPoint(this).Position.Y;
        Notes.PanelHeight = Math.Max(80, Math.Min(600, _dragStartNoteHeight + delta));
        e.Handled = true;
    }

    private void NotesSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingNotes = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void NotesSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingNotes = false;
    }

    private void BeginAddCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Parent: Grid footerGrid }) return;
        var textBox = footerGrid.Children.OfType<StackPanel>().FirstOrDefault()
                                ?.Children.OfType<TextBox>().FirstOrDefault();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => textBox?.Focus(FocusState.Programmatic));
    }

    private void AddCardTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LaneViewModel lane) return;
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = lane.ConfirmAddCardCommand.ExecuteAsync(null);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            lane.CancelAddCardCommand.Execute(null);
        }
    }

    private async void NotesTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.S) return;
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if (!ctrl.HasFlag(CoreVirtualKeyStates.Down)) return;
        e.Handled = true;
        await Notes.QuickSaveAsync();
    }
}
