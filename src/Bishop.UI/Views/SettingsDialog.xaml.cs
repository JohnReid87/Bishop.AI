using Bishop.App.Skills;
using Bishop.Core.Skills;
using Bishop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialogViewModel ViewModel { get; }

    public SettingsDialog(SettingsDialogViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
        Loaded += async (_, _) => await LoadSkillsAsync();
    }

    private async Task LoadSkillsAsync()
    {
        await ViewModel.Skills.LoadAsync();
        var workspacePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var skill in ViewModel.Skills.MetaSkills)
        {
            var capturedSkill = skill;
            var settingKey = skill.Name;
            var savedModel = await ViewModel.Skills.GetSkillModelAsync(settingKey);

            SkillsPanel.Children.Add(SkillRowFactory.MakeRow(
                skill.Name,
                savedModel,
                onLaunch: async chosenModel =>
                {
                    await ViewModel.Skills.SetSkillModelAsync(settingKey, chosenModel);
                    await LaunchMetaSkillAsync(capturedSkill, workspacePath, chosenModel);
                },
                onView: async () => await ShowSkillContentAsync(capturedSkill)));
        }

        if (ViewModel.Skills.MetaSkills.Count == 0)
            SkillsPanel.Children.Add(new TextBlock
            {
                Text = "No meta skills installed. Run `bishop install-skills`.",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppTextTertiaryBrush"],
                FontStyle = Windows.UI.Text.FontStyle.Italic,
            });
    }

    private async Task LaunchMetaSkillAsync(InstalledSkill skill, string workspacePath, string? modelId)
    {
        var command = string.IsNullOrWhiteSpace(skill.Command)
            ? $"/{skill.Name}"
            : SkillCommandRenderer.Render(skill.Command, null, null, null, workspacePath);

        if (SkillStaging.ShouldShowStageDialog(skill, hasCard: false))
        {
            var prefill = skill.StagePrefill is null
                ? null
                : SkillCommandRenderer.Render(skill.StagePrefill, null, null, null, workspacePath).Trim();
            var stageDialog = new SkillStageDialog(skill.Name, skill.StagePrompt, prefill?.Length > 0 ? prefill : null) { XamlRoot = XamlRoot };
            if (await stageDialog.ShowAsync() != ContentDialogResult.Primary) return;
            var input = stageDialog.InputText?.Trim() ?? string.Empty;
            if (input.Length > 0) command = $"{command} {input}";
        }

        await ViewModel.Skills.LaunchCommandAsync(workspacePath, command, SnapHelper.ComputeSnap(), modelId);
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Hide();

    private Task ShowSkillContentAsync(InstalledSkill skill)
    {
        App.MarkdownViewer!.ShowContent(skill.Name, skill.MarkdownBody);
        return Task.CompletedTask;
    }
}
