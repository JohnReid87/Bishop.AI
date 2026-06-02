using Bishop.UI.Views.Controls;
using Bishop.ViewModels.Skills;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Bishop.UI.Views.Skills;

internal static class SkillFlyoutFactory
{
    public static void Show(
        FrameworkElement anchor,
        IReadOnlyList<SkillLaunchItem> items,
        Func<SkillLaunchItem, string, Task> onLaunch,
        Action<SkillLaunchItem> onView)
    {
        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };

        foreach (var item in items)
        {
            if (item.GroupHeader is not null)
                panel.Children.Add(MakeCategoryHeader(item.GroupHeader));

            var captured = item;
            panel.Children.Add(SkillRowFactory.MakeRow(captured.Name, captured.SavedModelId,
                onLaunch: async chosenModel =>
                {
                    flyout.Hide();
                    await onLaunch(captured, chosenModel);
                },
                onView: () =>
                {
                    flyout.Hide();
                    onView(captured);
                    return Task.CompletedTask;
                }));
        }

        flyout.Content = panel;
        flyout.ShowAt(anchor);
    }

    private static FrameworkElement MakeCategoryHeader(string text) =>
        new TextBlock
        {
            Text = text,
            FontSize = 10,
            Opacity = 0.5,
            Margin = new Thickness(4, 6, 4, 2),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CharacterSpacing = 75,
        };
}
