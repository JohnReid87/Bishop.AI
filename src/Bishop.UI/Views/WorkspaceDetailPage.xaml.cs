using CommunityToolkit.WinUI.Controls;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Cards.PushLane;
using Bishop.App.Services.GitHub;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Git.Push;
using Bishop.UI.Services;
using Bishop.ViewModels;
using Bishop.App.Services.Settings;
using Bishop.App.Skills;
using Bishop.App.Skills.DiscoverSkills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Services.Terminal;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.App.WorkNext;
using Bishop.App.WorkNext.LaunchWorkNext;
using Bishop.Core.Skills;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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

namespace Bishop.UI.Views;

public sealed partial class WorkspaceDetailPage : Page
{
    private readonly DbChangeWatcher _dbWatcher;
    private WorkNextStateWatcher? _workNextWatcher;
    private WorkNextState _workNextState = new(false, false);
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
    private SkillMenuItem[] _cardSkills = [];
    private SkillMenuItem[] _workspaceSkills = [];


    public WorkspaceBoardViewModel Board { get; }
    public WorkspaceNotesViewModel Notes { get; }
    public WorkspaceMonitoringViewModel Monitoring { get; }

    public WorkspaceDetailPage()
    {
        Board = App.Services.GetRequiredService<WorkspaceBoardViewModel>();
        Notes = App.Services.GetRequiredService<WorkspaceNotesViewModel>();
        Monitoring = App.Services.GetRequiredService<WorkspaceMonitoringViewModel>();
        _dbWatcher = App.Services.GetRequiredService<DbChangeWatcher>();
        InitializeComponent();
        Board.Lanes.CollectionChanged += (_, _) => ApplyWorkNextStateToToDoLane();
        Board.Lanes.CollectionChanged += (_, _) => ApplyGitHubRepoToBacklogLane();
        Board.Lanes.CollectionChanged += (_, _) => ApplyGitHubRepoToDoneLane();
    }

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
        DisposeWorkNextWatcher();
        _ = Notes.FlushAsync();
        Notes.Dispose();
    }

    private void DisposeWorkNextWatcher()
    {
        if (_workNextWatcher is null) return;
        _workNextWatcher.StateChanged -= OnWorkNextStateChanged;
        _workNextWatcher.Dispose();
        _workNextWatcher = null;
        _workNextState = new WorkNextState(false, false);
    }

    private void SetupWorkNextWatcher(string workspacePath)
    {
        DisposeWorkNextWatcher();
        _workNextWatcher = new WorkNextStateWatcher(workspacePath);
        _workNextWatcher.StateChanged += OnWorkNextStateChanged;
        _workNextState = _workNextWatcher.CurrentState;
        ApplyWorkNextStateToToDoLane();
    }

    private void OnWorkNextStateChanged(object? sender, WorkNextState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _workNextState = state;
            ApplyWorkNextStateToToDoLane();
        });
    }

    private void ApplyWorkNextStateToToDoLane()
    {
        var todoLane = Board.Lanes.FirstOrDefault(l => l.IsToDoLane);
        if (todoLane is null) return;
        todoLane.IsWorkNextRunning = _workNextState.IsRunning;
        todoLane.IsWorkNextStopping = _workNextState.IsStopping;
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
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Board.RefreshCommand.ExecuteAsync(null);
            await Monitoring.RefreshCommand.ExecuteAsync(null);
        });
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Board.RefreshCommand.ExecuteAsync(null);
            await Monitoring.RefreshCommand.ExecuteAsync(null);
        });
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
            UpdatePathStatus();
            ApplyGitHubRepoToBacklogLane();
            ApplyGitHubRepoToDoneLane();
            await LoadSkillsAsync();
            SetupWorkNextWatcher(vm.Path);
            _ = Board.LoadAsync(vm.Id);
            _ = Notes.LoadAsync(vm.Id, vm.Path);
            _ = Monitoring.LoadAsync(vm.Id, vm.Path);
        });

    private async Task LoadSkillsAsync()
    {
        var mediator = App.Services.GetRequiredService<ISender>();
        var skills = await mediator.Send(new DiscoverSkillsQuery());
        _cardSkills = SkillMenuBuilder.Build(skills, "card");
        _workspaceSkills = SkillMenuBuilder.Build(skills, "workspace");
        CardViewModel.IsCardSkillsButtonVisible = _cardSkills.Length > 0;
        WorkspaceSkillsButton.Visibility = _workspaceSkills.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
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
            var mediator = App.Services.GetRequiredService<ISender>();
            var launchedWithTerminal = await mediator.Send(new LaunchWorkspaceCommand(_item.Path, SnapHelper.ComputeSnap()));
            FallbackWarningBar.IsOpen = !launchedWithTerminal;
            UpdateNotificationPanel();
        });

    private async void TerminalButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            var mediator = App.Services.GetRequiredService<ISender>();
            await mediator.Send(new LaunchPlainTerminalCommand(_item.Path, SnapHelper.ComputeSnap()));
        });

    private async void WorkspaceSkillsButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null || _workspaceSkills.Length == 0) return;
            var appSettings = App.Services.GetRequiredService<IAppSettings>();

            var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

            foreach (var item in _workspaceSkills)
            {
                if (item.GroupHeader is not null)
                    panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

                var skill = item.Skill;
                var rendered = SkillCommandRenderer.Render(skill.Command!, null, null, null, _item.Path);
                var workspacePath = _item.Path;
                var settingKey = $"skill.{skill.Name}.last_model";
                var savedModel = SkillModelOptions.ResolveModelId(await appSettings.GetAsync(settingKey));

                panel.Children.Add(SkillRowFactory.MakeRow(item.Name, savedModel,
                    onLaunch: async chosenModel =>
                    {
                        await appSettings.SetAsync(settingKey, chosenModel);
                        flyout.Hide();
                        await LaunchSkillAsync(skill, rendered, workspacePath, card: null, chosenModel);
                    },
                    onView: () =>
                    {
                        flyout.Hide();
                        App.MarkdownViewer!.ShowContent(skill.Name, skill.MarkdownBody);
                        return Task.CompletedTask;
                    }));
                if (item.HasSeparatorAfter)
                    panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
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
        var mediator = App.Services.GetRequiredService<ISender>();
        var result = await mediator.Send(new GetRecentCommitsQuery(workspacePath));

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4), Width = 420 };

        switch (result)
        {
            case GetRecentCommitsResult.Success { Commits: var commits, UpstreamRef: var upstreamRef }:
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

                void RenderCommits(IReadOnlyList<CommitInfo> currentCommits, string? currentUpstream)
                {
                    commitsContainer.Children.Clear();
                    foreach (var (commit, i) in currentCommits.Select((c, idx) => (c, idx)))
                    {
                        var capturedCommit = commit;
                        commitsContainer.Children.Add(MakeCommitRow(commit, currentUpstream, async () =>
                        {
                            flyout.Hide();
                            if (gitHubRepo is not null)
                            {
                                await Launcher.LaunchUriAsync(new Uri($"https://github.com/{gitHubRepo}/commit/{capturedCommit.FullHash}"));
                            }
                            else
                            {
                                var pkg = new DataPackage();
                                pkg.SetText(capturedCommit.FullHash);
                                Clipboard.SetContent(pkg);
                                await ShowCopiedToastAsync();
                            }
                        }));
                        if (i < currentCommits.Count - 1)
                            commitsContainer.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
                    }
                }

                void UpdatePushButton(IReadOnlyList<CommitInfo> currentCommits, string? currentUpstream)
                {
                    string label;
                    bool enabled;
                    if (currentUpstream is null)
                    {
                        label = "No upstream configured";
                        enabled = false;
                    }
                    else
                    {
                        var unpushed = currentCommits.Count(c => !c.IsPushed);
                        if (unpushed == 0)
                        {
                            label = "Up to date";
                            enabled = false;
                        }
                        else
                        {
                            label = $"Push {unpushed} commit{(unpushed == 1 ? "" : "s")}";
                            enabled = true;
                        }
                    }
                    pushButton.Content = new TextBlock { Text = label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
                    pushButton.IsEnabled = enabled;
                }

                pushButton.Click += async (_, _) =>
                {
                    errorBlock.Visibility = Visibility.Collapsed;
                    var previousContent = pushButton.Content;
                    pushButton.IsEnabled = false;
                    pushButton.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };

                    var pushResult = await mediator.Send(new PushCommand(workspacePath));
                    if (pushResult.Success)
                    {
                        var refreshed = await mediator.Send(new GetRecentCommitsQuery(workspacePath));
                        if (refreshed is GetRecentCommitsResult.Success { Commits: var refreshedCommits, UpstreamRef: var refreshedUpstream })
                        {
                            RenderCommits(refreshedCommits, refreshedUpstream);
                            UpdatePushButton(refreshedCommits, refreshedUpstream);
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
                };

                RenderCommits(commits, upstreamRef);
                UpdatePushButton(commits, upstreamRef);
                panel.Children.Add(commitsContainer);
                panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 4, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
                panel.Children.Add(errorBlock);
                panel.Children.Add(pushButton);
                break;
            case GetRecentCommitsResult.NotAGitRepo:
                panel.Children.Add(new TextBlock { Text = "Not a git repository", FontSize = 12, Padding = new Thickness(4, 6, 4, 6) });
                break;
            case GetRecentCommitsResult.GitNotFound:
                panel.Children.Add(new TextBlock { Text = "Git not installed or not on PATH", FontSize = 12, Padding = new Thickness(4, 6, 4, 6) });
                break;
            case GetRecentCommitsResult.NoCommits:
                panel.Children.Add(new TextBlock { Text = "No commits yet", FontSize = 12, Padding = new Thickness(4, 6, 4, 6) });
                break;
        }

        flyout.Content = panel;
        flyout.ShowAt((FrameworkElement)sender);
        });

    private async Task ShowCopiedToastAsync()
    {
        CopiedBar.IsOpen = true;
        UpdateNotificationPanel();
        await Task.Delay(2000);
        CopiedBar.IsOpen = false;
        UpdateNotificationPanel();
    }

    private static FrameworkElement MakeCommitRow(CommitInfo commit, string? upstreamRef, Func<Task> onClick)
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
            iconTooltip = "No upstream configured";
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
        btn.Click += async (_, _) => await onClick();

        var tooltipText = string.IsNullOrEmpty(commit.Body) ? commit.Subject : $"{commit.Subject}\n\n{commit.Body}";
        if (commit.IsPushed && upstreamRef is not null)
            tooltipText += $"\n\nPushed to {upstreamRef}";
        ToolTipService.SetToolTip(btn, new TextBlock { Text = tooltipText, TextWrapping = TextWrapping.Wrap, MaxWidth = 600 });

        return btn;
    }

    private static string GetRelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();
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

            var dialog = new CardDetailDialog(card, _cardSkills, _item?.Path ?? string.Empty, _item?.Id ?? Guid.Empty, _item?.GitHubRepo) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (dialog.ViewModel.Deleted || dialog.ViewModel.Updated)
                await Board.RefreshCommand.ExecuteAsync(null);
        });

    private async void CardSkillsButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null || _cardSkills.Length == 0) return;
            if (GetCardFromSender(sender) is not CardViewModel card) return;
            var appSettings = App.Services.GetRequiredService<IAppSettings>();

            var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

            foreach (var item in _cardSkills)
            {
                if (item.GroupHeader is not null)
                    panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

                var skill = item.Skill;
                var rendered = SkillCommandRenderer.Render(skill.Command!, card?.Number, card?.Title, card?.Description, _item.Path);
                var workspacePath = _item.Path;
                var settingKey = $"skill.{skill.Name}.last_model";
                var savedModel = SkillModelOptions.ResolveModelId(await appSettings.GetAsync(settingKey));

                panel.Children.Add(SkillRowFactory.MakeRow(item.Name, savedModel,
                    onLaunch: async chosenModel =>
                    {
                        await appSettings.SetAsync(settingKey, chosenModel);
                        flyout.Hide();
                        await LaunchSkillAsync(skill, rendered, workspacePath, card, chosenModel);
                    },
                    onView: () =>
                    {
                        flyout.Hide();
                        App.MarkdownViewer!.ShowContent(skill.Name, skill.MarkdownBody);
                        return Task.CompletedTask;
                    }));
                if (item.HasSeparatorAfter)
                    panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
            }

            flyout.Content = panel;
            flyout.ShowAt((FrameworkElement)sender);
        });

    private async void CardCloseButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (GetCardFromSender(sender) is not CardViewModel card) return;
            var mediator = App.Services.GetRequiredService<ISender>();

            if (card.IsClosed)
                await mediator.Send(new ReopenCardCommand(card.Id));
            else
                await mediator.Send(new CloseCardCommand(card.Id));

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
            };
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

        var mediator = App.Services.GetRequiredService<ISender>();
        IReadOnlyList<Bishop.Core.TagInfo> allTags;
        try
        {
            allTags = await mediator.Send(new ListTagsByWorkspaceQuery(_item.Id));
        }
        catch
        {
            return;
        }

        var flyout = TagPickerFlyout.Build(allTags, [], async (name, _) =>
        {
            await mediator.Send(new UpdateCardCommand(card.Id, null, null, true, name));
            await Board.RefreshCommand.ExecuteAsync(null);
        }, currentlySelected);
        flyout.ShowAt(anchor);
    }

    private async Task LaunchSkillAsync(InstalledSkill skill, string rendered, string workspacePath, CardViewModel? card, string? modelId = null)
    {
        if (SkillStaging.ShouldShowStageDialog(skill, card is not null))
        {
            var prefill = skill.StagePrefill is null
                ? null
                : SkillCommandRenderer.Render(skill.StagePrefill, card?.Number, card?.Title, card?.Description, workspacePath).Trim();
            var initialText = string.IsNullOrEmpty(prefill) ? null : prefill;
            var dialog = new SkillStageDialog(skill.Name, skill.StagePrompt, initialText) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var input = dialog.InputText?.Trim() ?? string.Empty;
            if (input.Length > 0)
                rendered = $"{rendered} {input}";
        }

        var mediator = App.Services.GetRequiredService<ISender>();
        await mediator.Send(new LaunchSkillCommand(workspacePath, rendered, SnapHelper.ComputeSnap(), modelId));
    }

    private async void RunNowSkillButton_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if ((sender as FrameworkElement)?.DataContext is not SkillRunRowViewModel row) return;
            var skillItem = _workspaceSkills.FirstOrDefault(s => string.Equals(s.Skill.Name, row.SkillName, StringComparison.OrdinalIgnoreCase));
            if (skillItem is null) return;
            var rendered = SkillCommandRenderer.Render(skillItem.Skill.Command!, null, null, null, _item.Path);
            await LaunchSkillAsync(skillItem.Skill, rendered, _item.Path, card: null, modelId: row.SelectedModelId);
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

            var mediator = App.Services.GetRequiredService<ISender>();
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
            const double EdgeZone = 48.0;
            const double MinSpeed = 80.0;
            const double MaxSpeed = 600.0;
            const double TickMs = 16.0;

            var pos = e.GetPosition(scrollViewer);
            var viewportHeight = scrollViewer.ViewportHeight;
            double velocity = 0;

            if (pos.Y < EdgeZone)
            {
                var depth = (EdgeZone - pos.Y) / EdgeZone;
                velocity = -(MinSpeed + (MaxSpeed - MinSpeed) * depth) * TickMs / 1000.0;
            }
            else if (pos.Y > viewportHeight - EdgeZone)
            {
                var depth = (pos.Y - (viewportHeight - EdgeZone)) / EdgeZone;
                velocity = (MinSpeed + (MaxSpeed - MinSpeed) * depth) * TickMs / 1000.0;
            }

            if (velocity != 0)
            {
                _autoScrollTarget = scrollViewer;
                _autoScrollVelocity = velocity;
                if (_autoScrollTimer is null)
                {
                    _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
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

            var mediator = App.Services.GetRequiredService<ISender>();
            await mediator.Send(new MoveCardCommand(card.Id, targetLaneName, position));
            await Board.RefreshCommand.ExecuteAsync(null);
        });

    private static int GetDropIndex(ItemsRepeater? repeater, DragEventArgs e, LaneViewModel targetLane)
    {
        if (repeater is null) return targetLane.FilteredCards.Count + 1;

        var dropPoint = e.GetPosition(repeater);
        for (var i = 0; i < targetLane.FilteredCards.Count; i++)
        {
            if (repeater.TryGetElement(i) is not FrameworkElement item) continue;
            var itemTop = item.TransformToVisual(repeater).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            if (dropPoint.Y < itemTop + item.ActualHeight / 2)
                return i + 1;
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

    private async void WorkNext_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item is null) return;
            if ((sender as FrameworkElement)?.DataContext is not LaneViewModel lane) return;
            if (!lane.CanWorkNext) return;

            var mediator = App.Services.GetRequiredService<ISender>();
            var appSettings = App.Services.GetRequiredService<IAppSettings>();
            var tags = await mediator.Send(new ListTagsByWorkspaceQuery(_item.Id));
            var lastModel = SkillModelOptions.ResolveModelId(await appSettings.GetAsync("workNext.last_model"));

            var dialog = new WorkNextOptionsDialog(tags.Select(t => t.Name), lastModel) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var chosenModel = dialog.ViewModel.SelectedModelId;
            await appSettings.SetAsync("workNext.last_model", chosenModel);
            await mediator.Send(new LaunchWorkNextCommand(
                _item.Path,
                dialog.ViewModel.SelectedTagOrNull,
                dialog.ViewModel.MaxValue,
                SnapHelper.ComputeSnap(),
                chosenModel));
        });

    private void WorkNextStop_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LaneViewModel lane) return;
        if (_workNextWatcher is null) return;
        _workNextWatcher.RequestStop();
        lane.IsWorkNextStopping = true;
    }

    private async void ImportFromGitHub_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item?.GitHubRepo is not { } repo) return;
            var mediator = App.Services.GetRequiredService<ISender>();
            var ghCli = App.Services.GetRequiredService<IGhCli>();
            var dialog = new ImportFromGitHubDialog(_item.Id, repo, mediator, ghCli) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (dialog.ViewModel.WasImported)
                await Board.RefreshCommand.ExecuteAsync(null);
        });

    private async void PushToGitHub_Click(object sender, RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            if (_item?.GitHubRepo is null) return;
            var doneLane = Board.Lanes.FirstOrDefault(l => l.IsDoneLane);
            if (doneLane is null) return;
            var mediator = App.Services.GetRequiredService<ISender>();
            var dialog = new PushLaneToGitHubDialog(_item.Id, doneLane.Name, doneLane.Cards.ToList(), mediator) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (dialog.ViewModel.WasPushed)
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
            _ = lane.ConfirmAddCardCommand.ExecuteAsync(null);
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
