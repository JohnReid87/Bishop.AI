using Bishop.ViewModels.Batches;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Bishop.UI.Views.Workspaces;

internal static class BatchMergeFailureFlyout
{
    public static void Show(FrameworkElement anchor, BatchMergeOutcome result)
    {
        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 4, Padding = new Thickness(8) };
        if (result.ConflictFiles.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Merge conflicts — resolve and re-run:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            foreach (var file in result.ConflictFiles)
                panel.Children.Add(new TextBlock { Text = file, FontSize = 12 });
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = result.ErrorMessage ?? "Merge failed.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            });
        }
        flyout.Content = panel;
        flyout.ShowAt(anchor);
    }
}
