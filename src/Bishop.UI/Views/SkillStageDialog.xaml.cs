using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class SkillStageDialog : ContentDialog
{
    private const string FullWorkspaceLabel = "(full workspace)";

    public SkillStageDialog(
        string skillName,
        string? customPrompt,
        string? initialText = null,
        bool stageProjects = false,
        string? workspacePath = null)
    {
        InitializeComponent();
        Title = $"Stage /{skillName}";
        PromptText.Text = customPrompt ?? "Optional input appended to the command before launch. Leave blank to run with no arguments.";
        if (!string.IsNullOrEmpty(initialText))
            InputBox.Text = initialText;

        if (stageProjects && !string.IsNullOrWhiteSpace(workspacePath))
            PopulateProjects(workspacePath);
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
}
