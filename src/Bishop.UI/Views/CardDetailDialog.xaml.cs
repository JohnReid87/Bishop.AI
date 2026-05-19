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
using Windows.System;
using Windows.UI.Core;

namespace Bishop.UI.Views;

public sealed partial class CardDetailDialog : ContentDialog
{
    private readonly IReadOnlyList<InstalledSkill> _cardSkills;
    private readonly string _workspacePath;

    public CardDetailDialogViewModel ViewModel { get; }

    public CardDetailDialog(CardViewModel card, IReadOnlyList<InstalledSkill> cardSkills, string workspacePath)
    {
        var mediator = App.Services.GetRequiredService<IMediator>();
        _cardSkills = cardSkills;
        _workspacePath = workspacePath;
        ViewModel = new CardDetailDialogViewModel(card, cardSkills, mediator);
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardDetailDialogViewModel.Deleted) && ViewModel.Deleted)
                Hide();
        };
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        ViewModel.RequestDeleteCommand.Execute(null);
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

    // ── Skills ────────────────────────────────────────────────────────────────

    private void SkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cardSkills.Count == 0) return;
        var flyout = new MenuFlyout();
        foreach (var skill in _cardSkills)
        {
            var rendered = RenderCommand(skill.Command!, ViewModel.Number, _workspacePath);
            var capturedSkill = skill;
            var item = new MenuFlyoutItem { Text = skill.Name };
            item.Click += async (_, _) => await LaunchSkillAsync(capturedSkill, rendered);
            flyout.Items.Add(item);
        }
        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task LaunchSkillAsync(InstalledSkill skill, string rendered)
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
        await mediator.Send(new LaunchSkillCommand(_workspacePath, rendered, ComputeSnap()));
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
