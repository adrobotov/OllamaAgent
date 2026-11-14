using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OllamaAgent.Helpers;

namespace OllamaAgent.Controls;

/// <summary>
/// A control that renders Markdown-formatted text with rich formatting
/// </summary>
public class MarkdownTextBlock : Panel
{
    public static readonly StyledProperty<string> TextProperty =
        TextBlock.TextProperty.AddOwner<MarkdownTextBlock>();

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MarkdownTextBlock()
    {
        TextProperty.Changed.AddClassHandler<MarkdownTextBlock>((_, e) => RenderMarkdown());
    }

    private void RenderMarkdown()
    {
        var segments = MarkdownRenderer.Render(Text ?? string.Empty);
        
        // Clear existing children
        Children.Clear();
        
        var stackPanel = new StackPanel { Spacing = 0 };
        
        foreach (var segment in segments.Where(s => !string.IsNullOrEmpty(s.Text)))
        {
            var tb = new TextBlock
            {
                Text = segment.Text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = segment.IsCode 
                    ? new FontFamily("Consolas, monospace") 
                    : new FontFamily("Inter, sans-serif"),
                FontWeight = segment.IsBold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = segment.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                FontSize = 14
            };
            
            // Set foreground color for code blocks
            if (segment.IsCode)
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                // Wrap in Border for background
                var border = new Border
                {
                    Child = tb,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245), 0.8),
                    Padding = new Thickness(8, 4, 8, 4),
                    CornerRadius = new CornerRadius(4)
                };
                stackPanel.Children.Add(border);
            }
            else
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                stackPanel.Children.Add(tb);
            }
        }
        
        Children.Add(stackPanel);
    }
}
