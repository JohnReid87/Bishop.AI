using Microsoft.UI.Xaml;

namespace Bishop.UI.Converters;

/// <summary>
/// Static helpers for x:Bind function-style conversions. Used in MainWindow.xaml,
/// where the Window-rooted binding scope can't resolve StaticResource converters
/// (Window is not a FrameworkElement, so the binding compiler's converter lookup
/// root requirement fails).
/// </summary>
public static class XamlConvert
{
    public static Visibility ToVis(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToVisInverse(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;
}
