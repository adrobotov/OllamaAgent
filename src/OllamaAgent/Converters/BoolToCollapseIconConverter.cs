using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OllamaAgent.Converters;

/// <summary>
/// Converts bool to collapse/expand icon
/// </summary>
public class BoolToCollapseIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCollapsed)
            return isCollapsed ? "▼" : "▲";
        return "▼";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ConvertBack not needed for this converter
        return null;
    }
}
