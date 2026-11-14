using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OllamaAgent.Converters;

/// <summary>
/// Converts bool to background brush for selected session
/// </summary>
public class SessionSelectedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            return isSelected 
                ? new SolidColorBrush(Color.FromRgb(50, 50, 50), 0.3)
                : Brushes.Transparent;
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
