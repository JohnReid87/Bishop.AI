using Bishop.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Bishop.UI.Views;

internal static class TagPickerFlyout
{
    internal static Flyout Build(
        IReadOnlyList<Tag> allTags,
        IReadOnlyCollection<string> alreadyAssigned,
        Func<string, string, Task> onPick)
    {
        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        var panel = new StackPanel { Spacing = 4, Width = 220, Padding = new Thickness(6) };
        var searchBox = new TextBox { PlaceholderText = "Search tags…" };
        var tagListPanel = new StackPanel { Spacing = 2 };

        panel.Children.Add(searchBox);
        panel.Children.Add(tagListPanel);
        flyout.Content = panel;

        void RefreshList(string filter)
        {
            tagListPanel.Children.Clear();
            var excluded = new HashSet<string>(alreadyAssigned, StringComparer.OrdinalIgnoreCase);
            var matches = allTags
                .Where(t => !excluded.Contains(t.Name) &&
                            (filter.Length == 0 || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var tag in matches)
            {
                var capturedTag = tag;
                tagListPanel.Children.Add(MakeTagRow(tag.Name, tag.Colour, async () =>
                {
                    flyout.Hide();
                    await onPick(capturedTag.Name, capturedTag.Colour);
                }));
            }

            if (matches.Count == 0)
            {
                tagListPanel.Children.Add(new TextBlock
                {
                    Text = "No matching tags",
                    Opacity = 0.5,
                    Margin = new Thickness(4, 2, 0, 2),
                });
            }
        }

        searchBox.TextChanged += (_, _) => RefreshList(searchBox.Text);
        flyout.Opened += (_, _) => searchBox.Focus(FocusState.Programmatic);
        RefreshList(string.Empty);

        return flyout;
    }

    private static Button MakeTagRow(string label, string colour, Func<Task> onSelect)
    {
        var dot = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(2),
            Background = BrushFromHex(colour),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(dot);
        row.Children.Add(text);

        var btn = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 3, 6, 3),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        btn.Click += async (_, _) => await onSelect();
        return btn;
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        hex = hex.TrimStart('#').PadRight(6, '0');
        if (hex.Length == 6) hex = "FF" + hex;
        return new SolidColorBrush(Windows.UI.Color.FromArgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16)));
    }
}
