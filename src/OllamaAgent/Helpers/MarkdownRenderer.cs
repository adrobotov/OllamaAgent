using System.Collections.Generic;
using System.Text;
using Markdig;

namespace OllamaAgent.Helpers;

/// <summary>
/// Converts Markdown to formatted text segments for display
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Renders Markdown to a list of formatted text segments
    /// </summary>
    public static List<TextSegment> Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new List<TextSegment> { new TextSegment { Text = markdown, IsBold = false, IsItalic = false, IsCode = false } };

        var segments = new List<TextSegment>();
        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var codeBuffer = new StringBuilder();

        foreach (var line in lines)
        {
            // Handle code blocks
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    segments.Add(new TextSegment { Text = codeBuffer.ToString(), IsCode = true, IsBold = false, IsItalic = false });
                    codeBuffer.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    if (codeBuffer.Length > 0)
                    {
                        segments.Add(new TextSegment { Text = codeBuffer.ToString(), IsCode = false, IsBold = false, IsItalic = false });
                        codeBuffer.Clear();
                    }
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBuffer.AppendLine(line);
                continue;
            }

            // Handle headers
            if (line.StartsWith("### "))
            {
                segments.Add(new TextSegment { Text = line[4..], IsBold = true, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            else if (line.StartsWith("## "))
            {
                segments.Add(new TextSegment { Text = line[3..], IsBold = true, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            else if (line.StartsWith("# "))
            {
                segments.Add(new TextSegment { Text = line[2..], IsBold = true, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            // Handle blockquotes
            else if (line.StartsWith("> "))
            {
                segments.Add(new TextSegment { Text = "  ▎ " + line[2..], IsBold = false, IsItalic = true, IsCode = false });
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            // Handle horizontal rules
            else if (line.Trim() == "---" || line.Trim() == "***" || line.Trim() == "___")
            {
                segments.Add(new TextSegment { Text = "\n───\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            // Handle list items
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                segments.Add(new TextSegment { Text = "  • ", IsBold = false, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = line[2..], IsBold = false, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            else if (line.StartsWith("1. ") || line.StartsWith("2. ") || line.StartsWith("3. "))
            {
                var numEnd = line.IndexOf('.');
                segments.Add(new TextSegment { Text = $"{line[..(numEnd + 1)]} ", IsBold = false, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = line[(numEnd + 2)..], IsBold = false, IsItalic = false, IsCode = false });
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
            // Handle bold and italic
            else
            {
                var processedLine = ProcessInlineFormatting(line);
                foreach (var segment in processedLine)
                {
                    segments.Add(segment);
                }
                segments.Add(new TextSegment { Text = "\n", IsBold = false, IsItalic = false, IsCode = false });
            }
        }

        // Handle unclosed code block
        if (codeBuffer.Length > 0)
        {
            segments.Add(new TextSegment { Text = codeBuffer.ToString(), IsCode = true, IsBold = false, IsItalic = false });
        }

        return segments;
    }

    private static List<TextSegment> ProcessInlineFormatting(string text)
    {
        var segments = new List<TextSegment>();
        var current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = false, IsCode = false };

        var i = 0;
        while (i < text.Length)
        {
            // Links [text](url)
            if (i < text.Length - 1 && text[i] == '[' && text[i + 1] != '!')
            {
                // Save current segment
                if (current.Text.Length > 0)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = false, IsCode = false };
                }

                // Find closing bracket
                var closeBracket = text.IndexOf(']', i + 1);
                if (closeBracket > 0 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    var linkText = text.Substring(i + 1, closeBracket - i - 1);
                    var closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > 0)
                    {
                        // segments.Add(new TextSegment { Text = "🔗 ", IsBold = false, IsItalic = true, IsCode = false });
                        segments.Add(new TextSegment { Text = linkText, IsBold = false, IsItalic = true, IsCode = false });
                        i = closeParen + 1;
                        continue;
                    }
                }
            }

            // Inline code
            if (text[i] == '`')
            {
                if (current.Text.Length > 0 && !current.IsCode)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = current.IsBold, IsItalic = current.IsItalic, IsCode = false };
                }
                else if (current.Text.Length > 0 && current.IsCode)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = false, IsCode = false };
                }
                else
                {
                    current.IsCode = !current.IsCode;
                    i++;
                    continue;
                }
            }

            // Bold
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                if (current.Text.Length > 0 && !current.IsBold)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = true, IsItalic = current.IsItalic, IsCode = current.IsCode };
                }
                else if (current.Text.Length > 0 && current.IsBold)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = current.IsItalic, IsCode = current.IsCode };
                }
                i += 2;
                continue;
            }

            // Italic
            if (text[i] == '*')
            {
                if (current.Text.Length > 0 && !current.IsItalic)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = current.IsBold, IsItalic = true, IsCode = current.IsCode };
                }
                else if (current.Text.Length > 0 && current.IsItalic)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = current.IsBold, IsItalic = false, IsCode = current.IsCode };
                }
                i++;
                continue;
            }

            current.Text += text[i];
            i++;
        }

        if (current.Text.Length > 0)
        {
            segments.Add(current);
        }

        return segments;
    }

    /// <summary>
    /// A single text segment with formatting
    /// </summary>
    public class TextSegment
    {
        public string Text { get; set; } = string.Empty;
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsCode { get; set; }
    }
}
