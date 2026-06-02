using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.Controls;

// WinUI 3 forbids opening a second ContentDialog while one is already active (COMException).
// Flyouts have no such restriction and anchor to the triggering element.
internal static class ConfirmFlyout
{
    public static Task<bool> ShowAsync(FrameworkElement anchor, string message, string verb)
    {
        var tcs = new TaskCompletionSource<bool>();

        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(4, 0, 4, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 260,
            FontSize = 13,
        });
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var confirmBtn = new Button { Content = verb };
        var cancelBtn = new Button { Content = "Cancel" };
        buttonRow.Children.Add(confirmBtn);
        buttonRow.Children.Add(cancelBtn);
        panel.Children.Add(buttonRow);

        var flyout = new Flyout { Content = panel };
        confirmBtn.Click += (_, _) => { flyout.Hide(); tcs.TrySetResult(true); };
        cancelBtn.Click += (_, _) => { flyout.Hide(); tcs.TrySetResult(false); };
        flyout.Closed += (_, _) => tcs.TrySetResult(false);
        flyout.ShowAt(anchor);

        return tcs.Task;
    }
}
