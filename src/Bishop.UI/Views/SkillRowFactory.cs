using Bishop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

internal static class SkillRowFactory
{
    internal static FrameworkElement MakeRow(
        string skillName,
        string selectedModelId,
        Func<string, Task> onLaunch,
        Func<Task>? onView = null)
    {
        var currentModelId = selectedModelId;
        var currentLabel = WorkNextOptionsDialogViewModel.Models
            .FirstOrDefault(m => m.Id == selectedModelId)?.Label ?? "Sonnet 4.6";

        var nameText = new TextBlock
        {
            Text = skillName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Width = 120,
            FontSize = 12,
        };

        var modelBtn = new Button
        {
            Content = $"{currentLabel} ▾",
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
            Width = 90,
            FontSize = 12,
        };

        var modelFlyout = new MenuFlyout();
        foreach (var (id, label) in WorkNextOptionsDialogViewModel.Models)
        {
            var capturedId = id;
            var capturedLabel = label;
            var mi = new MenuFlyoutItem { Text = label };
            mi.Click += (_, _) =>
            {
                currentModelId = capturedId;
                modelBtn.Content = $"{capturedLabel} ▾";
            };
            modelFlyout.Items.Add(mi);
        }
        modelBtn.Flyout = modelFlyout;

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        row.Children.Add(nameText);
        row.Children.Add(modelBtn);

        if (onView is not null)
        {
            var viewBtn = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(4, 2, 4, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 12,
            };
            ToolTipService.SetToolTip(viewBtn, "View SKILL.md");
            viewBtn.Click += async (_, _) => await onView();
            row.Children.Add(viewBtn);
        }

        var launchBtn = new Button
        {
            Content = "▶",
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        };
        launchBtn.Click += async (_, _) => await onLaunch(currentModelId);
        row.Children.Add(launchBtn);

        return row;
    }
}
