using Microsoft.UI.Xaml.Data;
using Windows.UI.Text;

namespace Bishop.UI.Converters;

public sealed class BoolToFontStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? FontStyle.Italic : FontStyle.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is FontStyle.Italic;
}
