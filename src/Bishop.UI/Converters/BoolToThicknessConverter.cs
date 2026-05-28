using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace Bishop.UI.Converters;

// ConverterParameter format: "trueThickness|falseThickness" using the XAML
// Thickness syntax (e.g. "10,2,4,2|10,4,4,4"). Lets ViewModels stay framework-
// neutral by exposing a plain bool while XAML decides the visual mapping.
public sealed class BoolToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var (whenTrue, whenFalse) = ParseParameter(parameter);
        return value is true ? whenTrue : whenFalse;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    private static (Thickness WhenTrue, Thickness WhenFalse) ParseParameter(object parameter)
    {
        if (parameter is not string raw)
            throw new ArgumentException("BoolToThicknessConverter requires a ConverterParameter of the form 'trueThickness|falseThickness'.", nameof(parameter));

        var parts = raw.Split('|');
        if (parts.Length != 2)
            throw new ArgumentException($"BoolToThicknessConverter expected 'trueThickness|falseThickness', got '{raw}'.", nameof(parameter));

        return (ParseThickness(parts[0]), ParseThickness(parts[1]));
    }

    private static Thickness ParseThickness(string text)
        => (Thickness)XamlBindingHelper.ConvertValue(typeof(Thickness), text);
}
