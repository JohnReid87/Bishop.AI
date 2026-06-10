using Bishop.UI.Views.Controls;
using Bishop.UI.Views.Skills;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI.Views.Settings;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly ISafeAsyncRunner _safeAsync;

    public SettingsDialogViewModel ViewModel { get; }

    public SettingsDialog(SettingsDialogViewModel vm)
    {
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        ViewModel = vm;
        InitializeComponent();
        Animations.EntranceAnimation.ApplyDialogEntrance(Content as FrameworkElement);
        Loaded += (_, _) => _safeAsync.RunAsync(LoadSkillsAsync);
    }

    private async Task LoadSkillsAsync()
    {
        await ViewModel.Skills.LoadAsync();

        foreach (var item in ViewModel.Skills.MetaSkills)
        {
            var captured = item;
            SkillsPanel.Children.Add(SkillRowFactory.MakeRow(
                captured.Name,
                captured.SavedModelId,
                onLaunch: chosenModel => LaunchAsync(captured, chosenModel),
                onView: () =>
                {
                    App.MarkdownViewer!.ShowContent(captured.Name, captured.MarkdownBody);
                    return Task.CompletedTask;
                }));
        }

        if (ViewModel.Skills.MetaSkills.Count == 0)
            SkillsPanel.Children.Add(new TextBlock
            {
                Text = "No meta skills installed. Run `bishop install-skills`.",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppTextTertiaryBrush"],
                FontStyle = Windows.UI.Text.FontStyle.Italic,
            });
    }

    private async Task LaunchAsync(SkillLaunchItem item, string chosenModel)
    {
        await ViewModel.Skills.SetSkillModelAsync(item.Name, chosenModel);

        string? stagedText = null;
        if (item.RequiresStage)
        {
            var dialog = new SkillStageDialog(item.Name, item.StagePrompt, item.StagePrefill) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            stagedText = dialog.InputText?.Trim();
        }

        await ViewModel.Skills.LaunchAsync(item, stagedText, SnapHelper.ComputeSnap(), chosenModel);
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Hide();

    private void SettingsSectionRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        if (GeneralContent is null) return;
        GeneralContent.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
        WorkspacesContent.Visibility = tag == "Workspaces" ? Visibility.Visible : Visibility.Collapsed;
        SkillsContent.Visibility = tag == "Skills" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void CopyDbPathButton_Click(object sender, RoutedEventArgs e)
    {
        var pkg = new DataPackage();
        pkg.SetText(ViewModel.DbPath);
        Clipboard.SetContent(pkg);

        CopiedBar.IsOpen = true;
        await Task.Delay(2000);
        CopiedBar.IsOpen = false;
    }
}
