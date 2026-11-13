using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OllamaAgent.ViewModels;

/// <summary>
/// ViewModel for a single chat message
/// </summary>
public partial class ChatMessageViewModel : ObservableObject
{
    public enum MessageType
    {
        User,
        Assistant,
        ToolResult,
        Error
    }

    [ObservableProperty]
    private string _content;

    [ObservableProperty]
    private MessageType _type;

    [ObservableProperty]
    private string _sender;

    [ObservableProperty]
    private Avalonia.Media.IBrush _backgroundColor = Avalonia.Media.Brushes.Transparent;

    [ObservableProperty]
    private Avalonia.Media.IBrush _senderColor = Avalonia.Media.Brushes.Transparent;

    public ChatMessageViewModel(string content, MessageType type)
    {
        Content = content;
        Type = type;
        
        Sender = type switch
        {
            MessageType.User => "Вы",
            MessageType.Assistant => "Ассистор",
            MessageType.ToolResult => "Инструмент",
            MessageType.Error => "Ошибка",
            _ => "Неизвестно"
        };

        // Set colors based on message type
        BackgroundColor = type switch
        {
            MessageType.User => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(219, 232, 241)),
            MessageType.Assistant => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(230, 244, 230)),
            MessageType.ToolResult => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 248, 220)),
            MessageType.Error => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 220, 220)),
            _ => Avalonia.Media.Brushes.Transparent
        };

        SenderColor = type switch
        {
            MessageType.User => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 100, 200)),
            MessageType.Assistant => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 150, 0)),
            MessageType.ToolResult => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(200, 150, 0)),
            MessageType.Error => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(200, 0, 0)),
            _ => Avalonia.Media.Brushes.Black
        };
    }
}
