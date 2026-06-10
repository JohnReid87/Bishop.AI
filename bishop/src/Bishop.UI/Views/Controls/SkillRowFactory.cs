using Bishop.ViewModels.Skills;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Bishop.UI.Views.Controls;

internal static class SkillRowFactory
{
    internal static FrameworkElement MakeRow(
        string skillName,
        string selectedModelId,
        Func<string, Task> onLaunch,
        Func<Task>? onView = null)
    {
        var currentModelId = selectedModelId;
        var currentLabel = SkillModels.All
            .FirstOrDefault(m => m.Id == selectedModelId)?.Label ?? "Sonnet 4.6";

        var nameText = new TextBlock
        {
            Text = skillName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
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
        foreach (var (id, label) in SkillModels.All)
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

        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(nameText, 0);
        row.Children.Add(nameText);

        Grid.SetColumn(modelBtn, 1);
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
            Grid.SetColumn(viewBtn, 2);
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
        Grid.SetColumn(launchBtn, 3);
        row.Children.Add(launchBtn);

        var dividerBrush = Application.Current.Resources["AppDividerBrush"] as Brush;
        return new Border
        {
            BorderBrush = dividerBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 4, 0, 4),
            Child = row,
        };
    }
}
