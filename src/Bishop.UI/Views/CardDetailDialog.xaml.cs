using Bishop.App.Settings;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Terminal;
using Bishop.Core.Skills;
using Bishop.UI.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
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
    private readonly IReadOnlyList<InstalledSkill> _cardSkills;
    private readonly string _workspacePath;

    private static readonly (string Id, string Label)[] Models =
    [
        ("claude-opus-4-7",           "Opus 4.7"),
        ("claude-sonnet-4-6",         "Sonnet 4.6"),
        ("claude-haiku-4-5-20251001", "Haiku 4.5"),
    ];
    private const string DefaultModel = "claude-sonnet-4-6";

    public CardDetailDialogViewModel ViewModel { get; }

    public CardDetailDialog(CardViewModel card, IReadOnlyList<InstalledSkill> cardSkills, string workspacePath, Guid workspaceId, string? gitHubRepo)
    {
        var mediator = App.Services.GetRequiredService<IMediator>();
        _cardSkills = cardSkills;
        _workspacePath = workspacePath;
        ViewModel = new CardDetailDialogViewModel(card, cardSkills, workspaceId, gitHubRepo, mediator);
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardDetailDialogViewModel.Deleted) && ViewModel.Deleted)
                Hide();
        };
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Hide();

    private async void GitHubIssueButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GitHubIssueUrl is { } url)
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

    private async void RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CardTagViewModel tag)
            await ViewModel.RemoveTagAsync(tag);
    }

    private async void AddTag_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<Bishop.Core.Tag> allTags;
        try
        {
            allTags = await ViewModel.GetWorkspaceTagsAsync();
        }
        catch
        {
            ViewModel.EditError = "Failed to load tags.";
            return;
        }

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 4, Width = 220, Padding = new Thickness(6) };
        var searchBox = new TextBox { PlaceholderText = "Search tags…" };
        var tagListPanel = new StackPanel { Spacing = 2 };

        panel.Children.Add(searchBox);
        panel.Children.Add(tagListPanel);
        flyout.Content = panel;

        void RefreshList(string filter)
        {
            tagListPanel.Children.Clear();
            var already = ViewModel.Tags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matches = allTags
                .Where(t => !already.Contains(t.Name) &&
                            (filter.Length == 0 || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var tag in matches)
            {
                var capturedTag = tag;
                tagListPanel.Children.Add(MakeTagRow(tag.Name, tag.Colour, async () =>
                {
                    flyout.Hide();
                    await ViewModel.AddTagAsync(capturedTag.Name, capturedTag.Colour);
                }));
            }

            var trimmed = filter.Trim();
            if (trimmed.Length > 0 &&
                !matches.Any(t => t.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) &&
                !already.Contains(trimmed))
            {
                tagListPanel.Children.Add(MakeTagRow($"Create \"{trimmed}\"", "#888888", async () =>
                {
                    flyout.Hide();
                    await ViewModel.AddTagAsync(trimmed, "#888888");
                }));
            }
        }

        searchBox.TextChanged += (_, _) => RefreshList(searchBox.Text);
        RefreshList(string.Empty);

        flyout.ShowAt((FrameworkElement)sender);
        searchBox.Focus(FocusState.Programmatic);
    }

    private static Button MakeTagRow(string label, string colour, Func<Task> onSelect)
    {
        var dot = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(2),
            Background = BrushFromHex(colour),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(dot);
        row.Children.Add(text);

        var btn = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 3, 6, 3),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        btn.Click += async (_, _) => await onSelect();
        return btn;
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        hex = hex.TrimStart('#').PadRight(6, '0');
        if (hex.Length == 6) hex = "FF" + hex;
        return new SolidColorBrush(Windows.UI.Color.FromArgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16)));
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    private async void SkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cardSkills.Count == 0) return;
        var appSettings = App.Services.GetRequiredService<IAppSettings>();

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

        foreach (var (skill, i) in _cardSkills.Select((s, i) => (s, i)))
        {
            var rendered = RenderCommand(skill.Command!, ViewModel.Number, _workspacePath);
            var capturedSkill = skill;
            var settingKey = $"skill.{skill.Name}.last_model";
            var savedModel = await appSettings.GetAsync(settingKey) ?? DefaultModel;

            panel.Children.Add(MakeSkillRow(skill.Name, savedModel, async chosenModel =>
            {
                await appSettings.SetAsync(settingKey, chosenModel);
                flyout.Hide();
                await LaunchSkillAsync(capturedSkill, rendered, chosenModel);
            }));
            if (i < _cardSkills.Count - 1)
                panel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        }

        flyout.Content = panel;
        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task LaunchSkillAsync(InstalledSkill skill, string rendered, string? modelId = null)
    {
        if (skill.Stage)
        {
            var stageDialog = new SkillStageDialog(skill.Name, skill.StagePrompt) { XamlRoot = XamlRoot };
            if (await stageDialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var input = stageDialog.InputText?.Trim() ?? string.Empty;
            if (input.Length > 0)
                rendered = $"{rendered} {input}";
        }

        var mediator = App.Services.GetRequiredService<IMediator>();
        await mediator.Send(new LaunchSkillCommand(_workspacePath, rendered, ComputeSnap(), modelId));
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

    private static string RenderCommand(string template, int cardNumber, string workspacePath) =>
        template
            .Replace("{{workspace_path}}", workspacePath)
            .Replace("{{card_number}}", cardNumber.ToString());

    private static TerminalSnap ComputeSnap()
    {
        var display = DisplayArea.GetFromWindowId(App.MainWindow!.AppWindow.Id, DisplayAreaFallback.Primary);
        var wa = display.WorkArea;
        return TerminalSnap.RightHalf(wa.X, wa.Y, wa.Width, wa.Height);
    }
}
