using Bishop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Bishop.UI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialogViewModel ViewModel { get; }

    public SettingsDialog(SettingsDialogViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
        Loaded += (_, _) => SafeAsync.RunAsync(LoadSkillsAsync);
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
