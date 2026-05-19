using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Terminal;
using Bishop.Core.Skills;
using Bishop.UI.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
