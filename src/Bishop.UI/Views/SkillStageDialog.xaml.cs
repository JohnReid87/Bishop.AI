using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class SkillStageDialog : ContentDialog
{
    public SkillStageDialog(string skillName, string? customPrompt)
    {
        InitializeComponent();
        Title = $"Stage /{skillName}";
        PromptText.Text = customPrompt ?? "Optional input appended to the command before launch. Leave blank to run with no arguments.";
    }

    public string InputText => InputBox.Text;
}
