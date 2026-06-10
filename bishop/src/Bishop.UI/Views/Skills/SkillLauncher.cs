using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Skills;

internal sealed class SkillLauncher
{
    private readonly WorkspaceBoardViewModel _board;

    public SkillLauncher(WorkspaceBoardViewModel board)
    {
        _board = board;
    }

    public async Task LaunchAsync(SkillLaunchItem item, string modelId, XamlRoot xamlRoot)
    {
        string? stagedText = null;
        if (item.RequiresStage)
        {
            var dialog = new SkillStageDialog(
                item.Name,
                item.StagePrompt,
                item.StagePrefill,
                item.StageProjects,
                item.StageFilePicker,
                _board.WorkspacePath) { XamlRoot = xamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            stagedText = dialog.InputText?.Trim();
        }

        await _board.LaunchAsync(item, stagedText, Bishop.UI.SnapHelper.ComputeSnap(), modelId);
    }
}
