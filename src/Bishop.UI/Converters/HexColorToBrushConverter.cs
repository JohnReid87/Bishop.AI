using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Bishop.UI.Converters;

public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && TryParseHex(hex, out var color))
            return new SolidColorBrush(color);
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();

    internal static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        var s = hex.TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            color = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        if (s.Length == 8 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var argb))
        {
            color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            return true;
        }
        return false;
    }
}
