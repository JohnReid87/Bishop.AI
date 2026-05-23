using Bishop.App.Cards.GetCard;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using Bishop.App.Settings;
using Bishop.App.Skills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.Core;
using Bishop.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;
using Launcher = Windows.System.Launcher;

namespace Bishop.UI.Views;

public sealed partial class CardDetailDialog : ContentDialog
{
    private readonly SkillMenuItem[] _cardSkills;
    private readonly string _workspacePath;
    private readonly Guid _workspaceId;

    public CardDetailDialogViewModel ViewModel { get; }

    public CardDetailDialog(CardViewModel card, SkillMenuItem[] cardSkills, string workspacePath, Guid workspaceId, string? gitHubRepo)
    {
        var mediator = App.Services.GetRequiredService<IMediator>();
        _cardSkills = cardSkills;
        _workspacePath = workspacePath;
        _workspaceId = workspaceId;
        ViewModel = new CardDetailDialogViewModel(card, cardSkills, workspaceId, gitHubRepo, mediator);
        InitializeComponent();
        PreviewKeyDown += CardDetailDialog_PreviewKeyDown;
        Loaded += CardDetailDialog_Loaded;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardDetailDialogViewModel.Deleted) && ViewModel.Deleted)
                Hide();
        };
    }

    private async void CardDetailDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var mediator = App.Services.GetRequiredService<IMediator>();
            var card = await mediator.Send(new GetCardQuery(ViewModel.CardId));
            if (card is null) return;

            ViewModel.SetClaudeTotals(
                card.TotalInputTokens,
                card.TotalOutputTokens,
                card.ClaudeRunCount);

            var commitResult = await mediator.Send(new GetCardCommitQuery(ViewModel.Number, _workspacePath));
            if (commitResult is GetCardCommitResult.Found found)
                ViewModel.SetCommit(found.Commit);
        }
        catch
        {
            // Silent fail — row stays hidden.
        }
    }

    private void CardDetailDialog_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape) return;
        if (ViewModel.IsTitleEditing || ViewModel.IsDescriptionEditing) return;
        e.Handled = true;
        Hide();
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Hide();

    private async void GitHubIssueButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GitHubIssueUrl is { } url)
            await Launcher.LaunchUriAsync(new Uri(url));
    }

    private async void CommitButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CommitUrl is { } url)
            await Launcher.LaunchUriAsync(new Uri(url));
    }

    // ── Title editing ─────────────────────────────────────────────────────────

    private void TitleView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        TitleTextBox.Text = ViewModel.Title;
        ViewModel.StartTitleEdit();
        TitleTextBox.Focus(FocusState.Programmatic);
        TitleTextBox.SelectAll();
    }

    private async void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await ViewModel.CommitTitleAsync(TitleTextBox.Text);
    }

    private async void TitleTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
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
    }

    // ── Description editing ───────────────────────────────────────────────────

    private void DescriptionView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        DescriptionTextBox.Text = ViewModel.Description;
        ViewModel.StartDescriptionEdit();
        DescriptionTextBox.Focus(FocusState.Programmatic);
    }

    private async void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await ViewModel.CommitDescriptionAsync(DescriptionTextBox.Text);
    }

    private async void DescriptionTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
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
    }

    // ── Tag editing ───────────────────────────────────────────────────────────

    private async void ClearTag_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearTagAsync();
    }

    private async void AddTag_Click(object sender, RoutedEventArgs e)
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
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    private async void SkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cardSkills.Length == 0) return;
        var appSettings = App.Services.GetRequiredService<IAppSettings>();

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

        foreach (var item in _cardSkills)
        {
            if (item.GroupHeader is not null)
                panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

            var skill = item.Skill;
            var rendered = SkillCommandRenderer.Render(skill.Command!, ViewModel.Number, ViewModel.Title, ViewModel.Description, _workspacePath);
            var settingKey = $"skill.{skill.Name}.last_model";
            var savedModel = SkillModelOptions.ResolveModelId(await appSettings.GetAsync(settingKey));

            panel.Children.Add(MakeSkillRow(item.Name, savedModel, async chosenModel =>
            {
                await appSettings.SetAsync(settingKey, chosenModel);
                flyout.Hide();
                await LaunchSkillAsync(rendered, chosenModel);
            }));
            if (item.HasSeparatorAfter)
                panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        }

        flyout.Content = panel;
        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task LaunchSkillAsync(string rendered, string? modelId = null)
    {
        // Card-context launch: skip the stage dialog. The rendered command already
        // has {{card_number}} substituted, so any stage_prompt would be redundant.
        var mediator = App.Services.GetRequiredService<IMediator>();
        await mediator.Send(new LaunchSkillCommand(_workspacePath, rendered, SnapHelper.ComputeSnap(), modelId));
    }

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

    private static FrameworkElement MakeSkillRow(string skillName, string selectedModelId, Func<string, Task> onLaunch)
    {
        var currentModelId = selectedModelId;
        var currentLabel = WorkNextOptionsDialogViewModel.Models.FirstOrDefault(m => m.Id == selectedModelId)?.Label ?? "Sonnet 4.6";

        var nameText = new TextBlock
        {
            Text = skillName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Width = 120,
            FontSize = 12,
        };

        var modelBtn = new Button
        {
            Content = $"{currentLabel} ▾",
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
            Width = 90,
            FontSize = 12,
        };

        var modelFlyout = new MenuFlyout();
        foreach (var option in WorkNextOptionsDialogViewModel.Models)
        {
            var capturedId = option.Id;
            var capturedLabel = option.Label;
            var mi = new MenuFlyoutItem { Text = option.Label };
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

}
