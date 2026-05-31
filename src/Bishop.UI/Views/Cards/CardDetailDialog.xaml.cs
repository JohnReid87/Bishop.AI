using Bishop.UI.Views.Controls;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Shared;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;
using Launcher = Windows.System.Launcher;

namespace Bishop.UI.Views.Cards;

public sealed partial class CardDetailDialog : ContentDialog
{
    private readonly Stack<CardViewModel> _backStack = new();
    private readonly ILogger<CardDetailDialog> _logger;
    private readonly ISafeAsyncRunner _safeAsync;

    public CardDetailDialogViewModel ViewModel { get; }

    public CardDetailDialog(CardDetailDialogViewModel vm)
    {
        _logger = App.Services.GetRequiredService<ILogger<CardDetailDialog>>();
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        ViewModel = vm;
        InitializeComponent();
        PreviewKeyDown += CardDetailDialog_PreviewKeyDown;
        Loaded += CardDetailDialog_Loaded;
        DescriptionMarkdown.OnLinkClicked += DescriptionMarkdown_LinkClicked;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardDetailDialogViewModel.Deleted) && ViewModel.Deleted)
                Hide();
        };
    }

    private async void CardDetailDialog_Loaded(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(() => Task.WhenAll(
            ViewModel.LoadCardNumbersAsync(),
            ViewModel.LoadExtrasAsync()));

    private async void DescriptionMarkdown_LinkClicked(object? sender, LinkClickedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            const string scheme = "bishop://card/";
            var url = e.Uri.OriginalString;
            if (!url.StartsWith(scheme, StringComparison.Ordinal)) return;
            if (!int.TryParse(url[scheme.Length..], out var targetNumber)) return;

            e.Handled = true;

            var snapshot = BuildCurrentCardSnapshot();
            _backStack.Push(snapshot);

            try
            {
                var targetVm = await ViewModel.GetCardByNumberAsync(targetNumber, ViewModel.IsSkillsButtonVisible);
                if (targetVm is null)
                {
                    _backStack.TryPop(out _);
                    return;
                }

                ViewModel.NavigateTo(targetVm, canGoBack: true);
                await ViewModel.LoadExtrasAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Card navigation failed for target #{TargetNumber}; reverting back stack", targetNumber);
                _backStack.TryPop(out _);
            }
        });

    private async void BackButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (!_backStack.TryPop(out var previous)) return;
            ViewModel.NavigateTo(previous, canGoBack: _backStack.Count > 0);
            await ViewModel.LoadExtrasAsync();
        });

    private CardViewModel BuildCurrentCardSnapshot() => new()
    {
        Id = ViewModel.CardId,
        Number = ViewModel.Number,
        Title = ViewModel.Title,
        Description = ViewModel.Description,
        LaneName = ViewModel.LaneName,
        TagName = ViewModel.TagName,
        TagColour = ViewModel.TagColour,
        IsClosed = ViewModel.IsClosed,
        GitHubIssueNumber = ViewModel.GitHubIssueNumber,
        IsSkillsButtonVisible = ViewModel.IsSkillsButtonVisible,
    };

    private void CardDetailDialog_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape) return;
        if (ViewModel.IsTitleEditing || ViewModel.IsDescriptionEditing) return;
        e.Handled = true;
        Hide();
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Hide();

    private async void GitHubIssueButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (ViewModel.GitHubIssueUrl is { } url)
                await Launcher.LaunchUriAsync(new Uri(url));
        });

    private async void CommitButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (ViewModel.CommitUrl is { } url)
                await Launcher.LaunchUriAsync(new Uri(url));
        });

    // ── Title editing ─────────────────────────────────────────────────────────

    private void TitleView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        TitleTextBox.Text = ViewModel.Title;
        ViewModel.StartTitleEdit();
        TitleTextBox.Focus(FocusState.Programmatic);
        TitleTextBox.SelectAll();
    }

    private async void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(() => ViewModel.CommitTitleAsync(TitleTextBox.Text));

    private async void TitleTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await ViewModel.CommitTitleAsync(TitleTextBox.Text);
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                ViewModel.CancelTitleEdit();
            }
        });

    // ── Description editing ───────────────────────────────────────────────────

    private void DescriptionEdit_Click(object sender, RoutedEventArgs e)
    {
        DescriptionTextBox.Text = ViewModel.Description;
        ViewModel.StartDescriptionEdit();
        DescriptionTextBox.Focus(FocusState.Programmatic);
    }

    private async void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(() => ViewModel.CommitDescriptionAsync(DescriptionTextBox.Text));

    private async void DescriptionTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                ViewModel.CancelDescriptionEdit();
            }
            else if (e.Key == VirtualKey.Enter)
            {
                var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                if ((ctrl & CoreVirtualKeyStates.Down) != 0)
                {
                    e.Handled = true;
                    await ViewModel.CommitDescriptionAsync(DescriptionTextBox.Text);
                }
            }
        });

    // ── Tag editing ───────────────────────────────────────────────────────────

    private async void TagChip_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            IReadOnlyList<Bishop.Core.TagInfo> allTags;
            try
            {
                allTags = await ViewModel.GetWorkspaceTagsAsync();
            }
            catch
            {
                ViewModel.EditError = "Failed to load tags.";
                return;
            }

            var flyout = TagPickerFlyout.Build(allTags, [], async (name, colour) =>
                await ViewModel.SetTagAsync(name, colour), currentlySelected: ViewModel.TagName);
            flyout.ShowAt((FrameworkElement)sender);
        });

    private async void AddTag_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            IReadOnlyList<Bishop.Core.TagInfo> allTags;
            try
            {
                allTags = await ViewModel.GetWorkspaceTagsAsync();
            }
            catch
            {
                ViewModel.EditError = "Failed to load tags.";
                return;
            }

            var flyout = TagPickerFlyout.Build(allTags, [], async (name, colour) =>
                await ViewModel.SetTagAsync(name, colour));
            flyout.ShowAt((FrameworkElement)sender);
        });

    // ── Skills ────────────────────────────────────────────────────────────────

    private async void SkillButton_Click(object sender, RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            if (ViewModel.CardSkills.Length == 0) return;

            var items = await ViewModel.BuildSkillLaunchItemsAsync();
            var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

            foreach (var item in items)
            {
                if (item.GroupHeader is not null)
                    panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

                var captured = item;
                panel.Children.Add(SkillRowFactory.MakeRow(captured.Name, captured.SavedModelId, async chosenModel =>
                {
                    await ViewModel.SetSkillModelAsync(captured.Name, chosenModel);
                    flyout.Hide();
                    await ViewModel.LaunchAsync(captured, stagedText: null, SnapHelper.ComputeSnap(), chosenModel);
                }));
            }

            flyout.Content = panel;
            flyout.ShowAt((FrameworkElement)sender);
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


}
