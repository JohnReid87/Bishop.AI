using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Bishop.UI.Converters;

public sealed class HexColorToContrastForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush Dark = new(Colors.Black);
    private static readonly SolidColorBrush Light = new(Colors.White);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && HexColorToBrushConverter.TryParseHex(hex, out var color))
            return RelativeLuminance(color) > 0.179 ? Dark : Light;
        return Light;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();

    private static double RelativeLuminance(Windows.UI.Color c)
    {
        static double Linearise(byte channel)
        {
            var s = channel / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linearise(c.R) + 0.7152 * Linearise(c.G) + 0.0722 * Linearise(c.B);
    }
}
