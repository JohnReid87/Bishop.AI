using Bishop.App.Services.Settings;
using Bishop.App.Skills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.Core.Skills;
using Bishop.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly BishopSettingsViewModel _skillsVm;

    public SettingsDialogViewModel ViewModel { get; }

    public SettingsDialog()
    {
        ViewModel = new SettingsDialogViewModel();
        _skillsVm = App.Services.GetRequiredService<BishopSettingsViewModel>();
        InitializeComponent();
        Loaded += async (_, _) => await LoadSkillsAsync();
    }

    private async Task LoadSkillsAsync()
    {
        await _skillsVm.LoadAsync();
        var appSettings = App.Services.GetRequiredService<IAppSettings>();
        var workspacePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var skill in _skillsVm.MetaSkills)
        {
            var capturedSkill = skill;
            var settingKey = $"skill.{skill.Name}.last_model";
            var savedModel = SkillModelOptions.ResolveModelId(await appSettings.GetAsync(settingKey));

            SkillsPanel.Children.Add(SkillRowFactory.MakeRow(
                skill.Name,
                savedModel,
                onLaunch: async chosenModel =>
                {
                    await appSettings.SetAsync(settingKey, chosenModel);
                    await LaunchMetaSkillAsync(capturedSkill, workspacePath, chosenModel);
                },
                onView: async () => await ShowSkillContentAsync(capturedSkill)));
        }

        if (_skillsVm.MetaSkills.Count == 0)
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

        var mediator = App.Services.GetRequiredService<ISender>();
        await mediator.Send(new LaunchSkillCommand(workspacePath, command, SnapHelper.ComputeSnap(), modelId));
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Hide();

    private Task ShowSkillContentAsync(InstalledSkill skill)
    {
        App.MarkdownViewer!.ShowContent(skill.Name, skill.MarkdownBody);
        return Task.CompletedTask;
    }
}
