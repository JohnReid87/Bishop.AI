using CommunityToolkit.WinUI.Controls;
using Bishop.UI.Services;
using Bishop.UI.Views.Controls;
using Bishop.UI.Views.Skills;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.GitHub;
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
    private readonly TimeProvider _timeProvider;
    private WorkspaceItemViewModel? _item;
    private CardViewModel? _draggedCard;
    private LaneViewModel? _dragSourceLane;
    private LaneViewModel? _currentDropTargetLane;
    private bool _isDraggingNotes;
    private double _dragStartPageY;
    private double _dragStartNoteHeight;
    private DispatcherTimer? _autoScrollTimer;
    private ScrollViewer? _autoScrollTarget;
    private double _autoScrollVelocity;


    public WorkspaceBoardViewModel Board { get; }
    public WorkspaceNotesViewModel Notes { get; }
    public WorkspaceMonitoringViewModel Monitoring { get; }
    public WorkspaceBatchesViewModel Batches { get; }

    public WorkspaceDetailPage()
    {
        Board = App.Services.GetRequiredService<WorkspaceBoardViewModel>();
        Notes = App.Services.GetRequiredService<WorkspaceNotesViewModel>();
        Monitoring = App.Services.GetRequiredService<WorkspaceMonitoringViewModel>();
        Batches = App.Services.GetRequiredService<WorkspaceBatchesViewModel>();
        _dbWatcher = App.Services.GetRequiredService<DbChangeWatcher>();
        _dialogService = App.Services.GetRequiredService<IDialogService>();
        _logger = App.Services.GetRequiredService<ILogger<WorkspaceDetailPage>>();
        _timeProvider = App.Services.GetRequiredService<TimeProvider>();
        InitializeComponent();
        Board.Lanes.CollectionChanged += (_, _) => ApplyGitHubRepoToBacklogLane();
        Board.Lanes.CollectionChanged += (_, _) => ApplyGitHubRepoToDoneLane();
        Board.StagingTray.Cards.CollectionChanged += OnStagingTrayCardsChanged;
        Monitoring.ViewFindingsRequested += OnViewFindingsRequested;
        Monitoring.ViewReportRequested += OnViewReportRequested;
    }

    private void OnViewFindingsRequested(Bishop.ViewModels.Findings.FindingsPageNavArgs args)
        => Frame?.Navigate(typeof(Bishop.UI.Views.Findings.FindingsPage), args);

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
        if (App.MainWindow is not null)
            App.MainWindow.Activated -= OnWindowActivated;
        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;
        // Notes is Transient + IDisposable; flush pending edits then release the FileSystemWatcher.
        _ = SafeAsync.RunAsync(Notes.FlushAsync);
        Notes.Dispose();
    }

    private void ApplyGitHubRepoToBacklogLane()
    {
        var backlogLane = Board.Lanes.FirstOrDefault(l => l.IsBacklogLane);
        if (backlogLane is null) return;
        backlogLane.HasGitHubRepo = !string.IsNullOrEmpty(_item?.GitHubRepo);
    }

    private void ApplyGitHubRepoToDoneLane()
    {
        var doneLane = Board.Lanes.FirstOrDefault(l => l.IsDoneLane);
        if (doneLane is null) return;
        doneLane.HasGitHubRepo = !string.IsNullOrEmpty(_item?.GitHubRepo);
    }

    private void OnDatabaseChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () => await SafeAsync.RunAsync(async () =>
        {
            await Task.WhenAll(
                Board.RefreshCommand.ExecuteAsync(null),
                Monitoring.RefreshCommand.ExecuteAsync(null),
                Batches.RefreshCommand.ExecuteAsync(null));
        }));
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated) return;
        DispatcherQueue.TryEnqueue(async () => await SafeAsync.RunAsync(async () =>
        {
            await Task.WhenAll(
                Board.RefreshCommand.ExecuteAsync(null),
                Monitoring.RefreshCommand.ExecuteAsync(null),
                Batches.RefreshCommand.ExecuteAsync(null));
        }));
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceItemViewModel.IsPathMissing))
            UpdatePathStatus();
        if (e.PropertyName is nameof(WorkspaceItemViewModel.GitHubRepo))
        {
            ApplyGitHubRepoToBacklogLane();
            ApplyGitHubRepoToDoneLane();
        }
    }

    private async void UpdateView(WorkspaceItemViewModel vm)
        => await SafeAsync.RunAsync(async () =>
        {
            WorkspaceNameText.Text = vm.Name;
            WorkspacePathText.Text = vm.Path;
            ToolTipService.SetToolTip(WorkspacePathText, vm.Path);
            Board.WorkspacePath = vm.Path;
            UpdatePathStatus();
            ApplyGitHubRepoToBacklogLane();
            ApplyGitHubRepoToDoneLane();
            await LoadSkillsAsync();
            _ = SafeAsync.RunAsync(() => Board.LoadAsync(vm.Id));
            _ = SafeAsync.RunAsync(() => Notes.LoadAsync(vm.Id, vm.Path));
            _ = SafeAsync.RunAsync(() => Monitoring.LoadAsync(vm.Id, vm.Path, vm.GitHubRepo));
            _ = SafeAsync.RunAsync(() => Batches.LoadAsync(vm.Id, vm.Path));
        });

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
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var launchedWithTerminal = await Board.LaunchClaudeAsync(_item.Path, SnapHelper.ComputeSnap());
            FallbackWarningBar.IsOpen = !launchedWithTerminal;
            UpdateNotificationPanel();
        });

    private async void TerminalButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            await Board.LaunchTerminalAsync(_item.Path, SnapHelper.ComputeSnap());
        });

    private async void WorkspaceSkillsButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null || Board.WorkspaceSkills.Length == 0) return;

            var items = await Board.BuildWorkspaceSkillLaunchItemsAsync();
            var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

            foreach (var item in items)
            {
                if (item.GroupHeader is not null)
                    panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

                var captured = item;
                panel.Children.Add(SkillRowFactory.MakeRow(captured.Name, captured.SavedModelId,
                    onLaunch: async chosenModel =>
                    {
                        await Board.SetSkillModelAsync(captured.Name, chosenModel);
                        flyout.Hide();
                        await LaunchSkillAsync(captured, chosenModel);
                    },
                    onView: () =>
                    {
                        flyout.Hide();
                        App.MarkdownViewer!.ShowContent(captured.Name, captured.MarkdownBody);
                        return Task.CompletedTask;
                    }));
            }

            flyout.Content = panel;
            flyout.ShowAt((FrameworkElement)sender);
        });

    private async void CommitsButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
        if (_item is null) return;
        var workspacePath = _item.Path;
        var gitHubRepo = _item.GitHubRepo;
        var result = await Board.GetRecentCommitsAsync(workspacePath);

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4), Width = 420 };

        switch (result)
        {
            case RecentCommitsResult.Success { Commits: var commits, UpstreamRef: var upstreamRef, UpstreamIsTracked: var upstreamIsTracked, UnpushedCount: var unpushedCount }:
                var commitsContainer = new StackPanel { Spacing = 2 };

                var errorBlock = new TextBlock
                {
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE5, 0x57, 0x4B)),
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(4, 4, 4, 0),
                };

                var pushButton = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(4, 4, 4, 4),
                    Margin = new Thickness(0, 4, 0, 0),
                };

                RenderCommitsInto(commitsContainer, flyout, commits, upstreamRef, gitHubRepo);
                var needsSetUpstream = UpdatePushButton(pushButton, upstreamRef, upstreamIsTracked, unpushedCount);

                pushButton.Click += (_, _) => SafeAsync.RunAsync(async () =>
                {
                    errorBlock.Visibility = Visibility.Collapsed;
                    var previousContent = pushButton.Content;
                    pushButton.IsEnabled = false;
                    pushButton.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };

                    var pushResult = await Board.PushAsync(workspacePath, setUpstream: needsSetUpstream);
                    if (pushResult.Success)
                    {
                        var refreshed = await Board.GetRecentCommitsAsync(workspacePath);
                        if (refreshed is RecentCommitsResult.Success { Commits: var refreshedCommits, UpstreamRef: var refreshedUpstream, UpstreamIsTracked: var refreshedIsTracked, UnpushedCount: var refreshedUnpushedCount })
                        {
                            RenderCommitsInto(commitsContainer, flyout, refreshedCommits, refreshedUpstream, gitHubRepo);
                            needsSetUpstream = UpdatePushButton(pushButton, refreshedUpstream, refreshedIsTracked, refreshedUnpushedCount);
                        }
                        else
                        {
                            pushButton.Content = previousContent;
                            pushButton.IsEnabled = true;
                        }
                    }
                    else
                    {
                        errorBlock.Text = string.IsNullOrWhiteSpace(pushResult.Message) ? "Push failed" : $"Push failed: {pushResult.Message}";
                        errorBlock.Visibility = Visibility.Visible;
                        pushButton.Content = previousContent;
                        pushButton.IsEnabled = true;
                    }
                });

                panel.Children.Add(new ScrollViewer { MaxHeight = 400, Content = commitsContainer });
                panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 4, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
                panel.Children.Add(errorBlock);
                panel.Children.Add(pushButton);
                break;
            case RecentCommitsResult.NotAGitRepo:
                panel.Children.Add(new TextBlock { Text = "Not a git repository", FontSize = 12, Padding = new Thickness(4, 6, 4, 6) });
                break;
            case RecentCommitsResult.GitNotFound:
                panel.Children.Add(new TextBlock { Text = "Git not installed or not on PATH", FontSize = 12, Padding = new Thickness(4, 6, 4, 6) });
                break;
            case RecentCommitsResult.NoCommits:
                panel.Children.Add(new TextBlock { Text = "No commits yet", FontSize = 12, Padding = new Thickness(4, 6, 4, 6) });
                break;
        }

        flyout.Content = panel;
        flyout.ShowAt((FrameworkElement)sender);
        });

    private void RenderCommitsInto(StackPanel container, Flyout flyout, IReadOnlyList<CommitItem> commits, string? upstreamRef, string? gitHubRepo)
    {
        container.Children.Clear();
        for (var i = 0; i < commits.Count; i++)
        {
            var commit = commits[i];
            container.Children.Add(MakeCommitRow(commit, upstreamRef, async () =>
            {
                flyout.Hide();
                if (gitHubRepo is not null)
                {
                    await Launcher.LaunchUriAsync(new Uri($"https://github.com/{gitHubRepo}/commit/{commit.FullHash}"));
                }
                else
                {
                    var pkg = new DataPackage();
                    pkg.SetText(commit.FullHash);
                    Clipboard.SetContent(pkg);
                    await ShowCopiedToastAsync();
                }
            }));
            if (i < commits.Count - 1)
                container.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        }
    }

    private static bool UpdatePushButton(Button pushButton, string? upstreamRef, bool upstreamIsTracked, int unpushedCount)
    {
        string label;
        bool enabled;
        if (upstreamRef is null)
        {
            label = "No remote branch — push with -u to publish";
            enabled = false;
        }
        else if (unpushedCount == 0)
        {
            label = "Up to date";
            enabled = false;
        }
        else
        {
            label = upstreamIsTracked
                ? $"Push {unpushedCount} commit{(unpushedCount == 1 ? "" : "s")}"
                : $"Push {unpushedCount} commit{(unpushedCount == 1 ? "" : "s")} (will set upstream)";
            enabled = true;
        }
        pushButton.Content = new TextBlock { Text = label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
        pushButton.IsEnabled = enabled;
        return !upstreamIsTracked;
    }

    private async Task ShowCopiedToastAsync()
    {
        CopiedBar.IsOpen = true;
        UpdateNotificationPanel();
        await Task.Delay(2000);
        CopiedBar.IsOpen = false;
        UpdateNotificationPanel();
    }

    private FrameworkElement MakeCommitRow(CommitItem commit, string? upstreamRef, Func<Task> onClick)
    {
        var secondaryBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppTextSecondaryBrush"];

        var hashBlock = new TextBlock
        {
            Text = commit.ShortHash,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 56,
            Foreground = secondaryBrush,
        };

        var subjectBlock = new TextBlock
        {
            Text = commit.Subject,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var timeBlock = new TextBlock
        {
            Text = GetRelativeTime(commit.Timestamp),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 56,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 4, 0),
            Foreground = secondaryBrush,
        };

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // hash
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // subject
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // time
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // iconHost
        Grid.SetColumn(hashBlock, 0);
        Grid.SetColumn(subjectBlock, 1);
        Grid.SetColumn(timeBlock, 2);
        grid.Children.Add(hashBlock);
        grid.Children.Add(subjectBlock);
        grid.Children.Add(timeBlock);

        var accentBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppAccentBrush"];
        var cloudUpData = (string)Application.Current.Resources["IconCloudUpData"];
        var icon = (Microsoft.UI.Xaml.Shapes.Path)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            $"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            $"Data='{cloudUpData}' StrokeThickness='1.5' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' " +
            $"Width='12' Height='12' Stretch='Uniform' VerticalAlignment='Center'/>");
        icon.Stroke = commit.IsPushed && upstreamRef is not null ? accentBrush : secondaryBrush;
        string iconTooltip;
        if (upstreamRef is null)
            iconTooltip = "No remote branch";
        else if (commit.IsPushed)
            iconTooltip = "Pushed";
        else
            iconTooltip = "Not yet pushed";
        var iconHost = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            Padding = new Thickness(2),
            Child = icon,
        };
        ToolTipService.SetToolTip(iconHost, iconTooltip);
        Grid.SetColumn(iconHost, 3);
        grid.Children.Add(iconHost);

        var btn = new Button
        {
            Content = grid,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 4, 4, 4),
        };
        btn.Click += (_, _) => SafeAsync.RunAsync(onClick);

        var tooltipText = string.IsNullOrEmpty(commit.Body) ? commit.Subject : $"{commit.Subject}\n\n{commit.Body}";
        if (commit.IsPushed && upstreamRef is not null)
            tooltipText += $"\n\nPushed to {upstreamRef}";
        ToolTipService.SetToolTip(btn, new TextBlock { Text = tooltipText, TextWrapping = TextWrapping.Wrap, MaxWidth = 600 });

        return btn;
    }

    private string GetRelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = _timeProvider.GetUtcNow() - timestamp.ToUniversalTime();
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"{(int)elapsed.TotalDays}d ago";
        return $"{(int)(elapsed.TotalDays / 30)}mo ago";
    }

    private async void CardTitle_Tapped(object sender, TappedRoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;

            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            if (ctrl.HasFlag(CoreVirtualKeyStates.Down))
            {
                Board.ToggleCardSelection(card);
                return;
            }

            var vm = await _dialogService.ShowCardDetailDialogAsync(card, Board.CardSkills, _item?.Path ?? string.Empty, _item?.Id ?? Guid.Empty, _item?.GitHubRepo, XamlRoot);
            if (vm.Deleted || vm.Updated)
                await Board.RefreshCommand.ExecuteAsync(null);
        });

    private void OnStagingTrayCardsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (Board.StagingTray.Cards.Count > 0 && string.IsNullOrEmpty(Board.StagingTray.BaseBranch) && _item is not null)
            _ = SafeAsync.RunAsync(async () =>
            {
                var branch = await Board.GetCurrentBranchAsync(_item.Path);
                Board.StagingTray.BaseBranch = branch;
            });
    }

    private void StagingTrayCancel_Click(object sender, RoutedEventArgs e)
        => Board.ClearSelection();

    private async void StagingTrayCreate_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var tray = Board.StagingTray;
            var selectedCards = tray.Cards.ToList();
            if (selectedCards.Count == 0) return;

            var name = tray.Name.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var slug = BatchStagingTrayViewModel.Slugify(name);
            var branchName = string.IsNullOrWhiteSpace(tray.Branch)
                ? $"bishop/{slug}"
                : tray.Branch.Trim();

            var workspacePath = _item.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var repoName = Path.GetFileName(workspacePath);
            var parentDir = Path.GetDirectoryName(workspacePath)!;
            var worktreePath = Path.Combine(parentDir, $"{repoName}-bishop-worktrees", slug);

            await Batches.CreateAsync(
                _item.Id, _item.Path, name, branchName, worktreePath,
                selectedCards.Select(c => c.Number).ToArray(),
                tray.Model);

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
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null || Board.CardSkills.Length == 0) return;
            if (GetCardFromSender(sender) is not CardViewModel card) return;

            var items = await Board.BuildCardSkillLaunchItemsAsync(card);
            var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

            foreach (var item in items)
            {
                if (item.GroupHeader is not null)
                    panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

                var captured = item;
                panel.Children.Add(SkillRowFactory.MakeRow(captured.Name, captured.SavedModelId,
                    onLaunch: async chosenModel =>
                    {
                        await Board.SetSkillModelAsync(captured.Name, chosenModel);
                        flyout.Hide();
                        await LaunchSkillAsync(captured, chosenModel);
                    },
                    onView: () =>
                    {
                        flyout.Hide();
                        App.MarkdownViewer!.ShowContent(captured.Name, captured.MarkdownBody);
                        return Task.CompletedTask;
                    }));
            }

            flyout.Content = panel;
            flyout.ShowAt((FrameworkElement)sender);
        });

    private async void CardCloseButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;

            await Board.ToggleCardClosedAsync(card.Id, card.IsClosed);

            var lane = Board.Lanes.FirstOrDefault(l => string.Equals(l.Name, card.LaneName, StringComparison.OrdinalIgnoreCase));
            if (lane is null) return;
            var idx = lane.Cards.IndexOf(card);
            if (idx < 0) return;
            lane.Cards[idx] = new CardViewModel
            {
                Id = card.Id,
                Number = card.Number,
                Title = card.Title,
                Description = card.Description,
                LaneName = card.LaneName,
                TagName = card.TagName,
                TagColour = card.TagColour,
                IsClosed = !card.IsClosed,
                GitHubIssueNumber = card.GitHubIssueNumber,
                GitHubPushedAt = card.GitHubPushedAt,
                LastAutoRunFailedAt = card.LastAutoRunFailedAt,
                LastAutoRunSucceededAt = card.LastAutoRunSucceededAt,
                IsSkillsButtonVisible = Board.IsCardSkillsButtonVisible,
                BatchId = card.BatchId,
                BatchName = card.BatchName,
                BatchCreatedAt = card.BatchCreatedAt,
            };
            Board.RefreshLaneItems();
        });

    private async void CardTagChip_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;
            await OpenCardTagPickerAsync((FrameworkElement)sender, card, card.TagName);
        });

    private async void CardAddTagButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
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

    private async Task LaunchSkillAsync(SkillLaunchItem item, string modelId)
    {
        string? stagedText = null;
        if (item.RequiresStage)
        {
            var dialog = new SkillStageDialog(
                item.Name,
                item.StagePrompt,
                item.StagePrefill,
                item.StageProjects,
                Board.WorkspacePath) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            stagedText = dialog.InputText?.Trim();
        }

        await Board.LaunchAsync(item, stagedText, SnapHelper.ComputeSnap(), modelId);
    }

    private async void RunNowSkillButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if ((sender as FrameworkElement)?.DataContext is not SkillRunRowViewModel row) return;
            var item = await Board.BuildWorkspaceSkillLaunchItemAsync(row.SkillName);
            if (item is null) return;
            await LaunchSkillAsync(item, row.SelectedModelId);
        });

    private static FrameworkElement MakeCategoryHeader(string text) =>
        new TextBlock
        {
            Text = text,
            FontSize = 10,
            Opacity = 0.5,
            Margin = new Thickness(4, 6, 4, 2),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CharacterSpacing = 75,
        };

    private static BatchItemViewModel? GetBatchFromSender(object sender) =>
        (sender as FrameworkElement)?.DataContext as BatchItemViewModel;

    private async void BatchRun_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
        await Batches.LaunchBatch(_item.Path, batch.Name, batch.Model, SnapHelper.ComputeSnap());
    }

    private async void BatchResume_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
        await Batches.ResumeBatch(_item.Path, batch.Name, batch.Model, SnapHelper.ComputeSnap());
    }

    private async void BatchPause_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (GetBatchFromSender(sender) is not BatchItemViewModel batch) return;
            await Batches.RequestStopAsync(batch.Id);
        });

    private async void BatchMerge_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
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
            {
                var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
                var panel = new StackPanel { Spacing = 4, Padding = new Thickness(8) };
                if (result.ConflictFiles.Count > 0)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = "Merge conflicts — resolve and re-run:",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    foreach (var file in result.ConflictFiles)
                        panel.Children.Add(new TextBlock { Text = file, FontSize = 12 });
                }
                else
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = result.ErrorMessage ?? "Merge failed.",
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 400
                    });
                }
                flyout.Content = panel;
                flyout.ShowAt((FrameworkElement)sender);
            }
        });

    private async void BatchCleanUp_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
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
        => await SafeAsync.RunAsync(async () =>
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
        => await SafeAsync.RunAsync(async () =>
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
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var closed = Batches.Batches.Where(b => b.CanRemove).ToList();
            if (closed.Count == 0) return;
            var message = $"Remove {closed.Count} closed {(closed.Count == 1 ? "batch" : "batches")}? Cards stay on the board.";
            if (!await ConfirmFlyoutAsync((FrameworkElement)sender, message, "Remove")) return;
            await Batches.RemoveAllClosedAsync(closed);
            await Batches.RefreshCommand.ExecuteAsync(null);
        });

    private static Task<bool> ConfirmFlyoutAsync(FrameworkElement anchor, string message, string verb)
    {
        var tcs = new TaskCompletionSource<bool>();

        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(4, 0, 4, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 260,
            FontSize = 13,
        });
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var confirmBtn = new Button { Content = verb };
        var cancelBtn = new Button { Content = "Cancel" };
        buttonRow.Children.Add(confirmBtn);
        buttonRow.Children.Add(cancelBtn);
        panel.Children.Add(buttonRow);

        var flyout = new Flyout { Content = panel };
        confirmBtn.Click += (_, _) => { flyout.Hide(); tcs.TrySetResult(true); };
        cancelBtn.Click += (_, _) => { flyout.Hide(); tcs.TrySetResult(false); };
        flyout.Closed += (_, _) => tcs.TrySetResult(false);
        flyout.ShowAt(anchor);

        return tcs.Task;
    }

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
        => await SafeAsync.RunAsync(async () =>
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not BatchItemViewModel batch) return;
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await CommitBatchNameAsync(textBox, batch);
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                batch.IsNameEditing = false;
            }
        });

    private async void BatchNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not BatchItemViewModel batch) return;
            if (batch.IsNameEditing)
                await CommitBatchNameAsync(textBox, batch);
        });

    private async Task CommitBatchNameAsync(TextBox textBox, BatchItemViewModel batch)
    {
        var trimmed = textBox.Text.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == batch.Name)
        {
            batch.IsNameEditing = false;
            return;
        }
        batch.Name = await Batches.RenameAsync(batch.Name, trimmed);
        batch.IsNameEditing = false;
    }

    private async void WorkspaceSettingsButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
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

            var repo = repoBox.Text.Trim();
            if (string.IsNullOrEmpty(repo))
            {
                await Board.UnsetGitHubRepoAsync(_item.Id);
                _item.GitHubRepo = null;
            }
            else
            {
                await Board.SetGitHubRepoAsync(_item.Id, repo);
                _item.GitHubRepo = repo;
            }
        });

    private void ClearAllDropTargets()
    {
        if (_currentDropTargetLane is not null)
        {
            _currentDropTargetLane.IsDropTarget = false;
            _currentDropTargetLane = null;
        }
    }

    private void Card_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        _draggedCard = GetCardFromSender(sender);
        if (_draggedCard is null) return;
        _dragSourceLane = Board.Lanes.FirstOrDefault(l => string.Equals(l.Name, _draggedCard.LaneName, StringComparison.OrdinalIgnoreCase));
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(_draggedCard.Id.ToString());
        LanesListView.CanReorderItems = false;
    }

    private void Card_DropCompleted(UIElement sender, DropCompletedEventArgs e)
    {
        ClearAllDropTargets();
        StopAutoScroll();
        LanesListView.CanReorderItems = true;
    }

    private void Cards_DragOver(object sender, DragEventArgs e)
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
        if (scrollViewer is not null)
        {
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
    }

    private void Cards_DragLeave(object sender, DragEventArgs e)
    {
        ClearAllDropTargets();
        StopAutoScroll();
    }

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

    private async void Cards_Drop(object sender, DragEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_draggedCard is null || _dragSourceLane is null) return;
            var targetLane = (sender as FrameworkElement)?.DataContext as LaneViewModel;
            if (targetLane is null) return;

            ClearAllDropTargets();
            StopAutoScroll();

            var position = GetDropIndex(FindVisualChild<ItemsRepeater>(sender as DependencyObject), e, targetLane);
            var card = _draggedCard;
            var targetLaneName = targetLane.Name;

            _draggedCard = null;
            _dragSourceLane = null;

            await Board.MoveCardAsync(card.Id, targetLaneName, position);
            await Board.RefreshCommand.ExecuteAsync(null);
        });

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

    private async void ImportFromGitHub_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item?.GitHubRepo is not { } repo) return;
            var vm = await _dialogService.ShowImportFromGitHubDialogAsync(_item.Id, repo, XamlRoot);
            if (vm.WasImported)
                await Board.RefreshCommand.ExecuteAsync(null);
        });

    private async void PushToGitHub_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item?.GitHubRepo is null) return;
            var doneLane = Board.Lanes.FirstOrDefault(l => l.IsDoneLane);
            if (doneLane is null) return;
            var vm = await _dialogService.ShowPushLaneToGitHubDialogAsync(_item.Id, doneLane.Name, doneLane.Cards.ToList(), XamlRoot);
            if (vm.WasPushed)
                await Board.RefreshCommand.ExecuteAsync(null);
        });

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
            _ = SafeAsync.RunAsync(() => lane.ConfirmAddCardCommand.ExecuteAsync(null));
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            lane.CancelAddCardCommand.Execute(null);
        }
    }

    private async void NotesTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (e.Key != VirtualKey.S) return;
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            if (!ctrl.HasFlag(CoreVirtualKeyStates.Down)) return;
            e.Handled = true;
            await Notes.QuickSaveAsync();
        });
}
