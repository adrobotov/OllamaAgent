using Avalonia.Data;
using Avalonia.Data.Converters;
using OllamaAgent.Helpers;
using System;
using System.Globalization;

namespace OllamaAgent.Controls;

/// <summary>
/// Converter that pre-processes LaTeX for CSharpMath compatibility.
/// Usage in XAML: <mv:MarkdownViewer Markdown="{Binding Content, Converter={StaticResource LatexConverter}}"/>
/// </summary>
public class LatexConverter : IValueConverter
{
    public static LatexConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string markdown)
        {
            return LatexPreprocessor.Preprocess(markdown);
        }
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way converter only
        return null;
    }
}
