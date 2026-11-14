using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OllamaAgent.ViewModels;

namespace OllamaAgent.Converters;

/// <summary>
/// Shows visibility only for ToolResult messages
/// Returns 1 for ToolResult (visible), 0 otherwise (collapsed)
/// </summary>
public class MessageTypeToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatMessageViewModel.MessageType messageType)
            return messageType == ChatMessageViewModel.MessageType.ToolResult ? 1 : 0;
        return 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
