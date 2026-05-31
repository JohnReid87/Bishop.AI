using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Bishop.UI.Views.Skills;

public sealed partial class SkillStageDialog : ContentDialog
{
    private const string FullWorkspaceLabel = "(full workspace)";

    public SkillStageDialog(
        string skillName,
        string? customPrompt,
        string? initialText = null,
        bool stageProjects = false,
        bool stageFilePicker = false,
        string? workspacePath = null)
    {
        InitializeComponent();
        Title = $"Stage /{skillName}";
        PromptText.Text = customPrompt ?? "Optional input appended to the command before launch. Leave blank to run with no arguments.";
        if (!string.IsNullOrEmpty(initialText))
            InputBox.Text = initialText;

        if (stageProjects && !string.IsNullOrWhiteSpace(workspacePath))
            PopulateProjects(workspacePath);

        if (stageFilePicker)
            FilePickerButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    public string InputText => InputBox.Text;

    private void PopulateProjects(string workspacePath)
    {
        var srcDir = Path.Combine(workspacePath, "src");
        if (!Directory.Exists(srcDir))
            return;

        var projects = Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (projects.Count == 0)
            return;

        ProjectsCombo.Items.Add(FullWorkspaceLabel);
        foreach (var name in projects)
            ProjectsCombo.Items.Add(name);

        ProjectsCombo.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void ProjectsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsCombo.SelectedItem is not string selected)
            return;

        InputBox.Text = selected == FullWorkspaceLabel
            ? string.Empty
            : $"src/{selected}";
    }

    private async void FilePickerButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
            InputBox.Text = file.Path;
    }
}
