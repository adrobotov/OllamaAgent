using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OllamaAgent.Helpers;

/// <summary>
/// Converts Markdown to formatted text segments for display
/// </summary>
public static class MarkdownRenderer
{
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
        var inTable = false;
        var tableBuffer = new List<string>();

        foreach (var line in lines)
        {
            // Handle code blocks
            if (line.TrimStart().StartsWith("```"))
            {
                // Flush any pending table
                if (inTable && tableBuffer.Count > 0)
                {
                    segments.Add(new TextSegment { Text = RenderTable(tableBuffer), IsBold = false, IsItalic = false, IsCode = false });
                    tableBuffer.Clear();
                    inTable = false;
                }

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
                        segments.Add(new TextSegment { Text = codeBuffer.ToString(), IsBold = false, IsItalic = false, IsCode = false });
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

            // Handle tables
            if (line.Contains('|') && line.Trim().StartsWith('|'))
            {
                var trimmed = line.Trim();
                // Skip separator lines
                if (trimmed.Contains("---") || trimmed.Contains(":-"))
                    continue;

                var pipeCount = trimmed.Count(c => c == '|');
                if (pipeCount >= 2)
                {
                    if (!inTable)
                    {
                        inTable = true;
                        tableBuffer.Clear();
                    }
                    tableBuffer.Add(line.Trim('|').Trim());
                    continue;
                }
            }

            // Flush table if we're in one and this line doesn't look like a table row
            if (inTable && tableBuffer.Count > 0)
            {
                segments.Add(new TextSegment { Text = RenderTable(tableBuffer), IsBold = false, IsItalic = false, IsCode = false });
                tableBuffer.Clear();
                inTable = false;
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

        // Flush any remaining content
        if (codeBuffer.Length > 0)
        {
            segments.Add(new TextSegment { Text = codeBuffer.ToString(), IsCode = true, IsBold = false, IsItalic = false });
        }

        if (inTable && tableBuffer.Count > 0)
        {
            segments.Add(new TextSegment { Text = RenderTable(tableBuffer), IsBold = false, IsItalic = false, IsCode = false });
        }

        return segments;
    }

    /// <summary>
    /// Render a simple text-based table
    /// </summary>
    private static string RenderTable(List<string> rows)
    {
        if (rows.Count == 0) return string.Empty;

        // Calculate column widths
        var maxCols = rows.Max(r => r.Split('|').Length);
        var colWidths = new int[maxCols];

        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Split('|');
            for (var j = 0; j < cells.Length && j < maxCols; j++)
            {
                colWidths[j] = Math.Max(colWidths[j], cells[j].Trim().Length);
            }
        }

        var sb = new StringBuilder();
        var topBorder = "┌" + string.Join("┬", Enumerable.Range(0, maxCols).Select(j => new string('─', colWidths[j] + 2))) + "┐";
        sb.AppendLine("\n" + topBorder);

        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Split('|');
            sb.Append("│");
            for (var j = 0; j < maxCols; j++)
            {
                var cell = j < cells.Length ? cells[j].Trim() : string.Empty;
                sb.Append(" " + cell.PadRight(colWidths[j]) + " │");
            }
            sb.AppendLine();
        }

        sb.Append("└" + string.Join("┴", Enumerable.Range(0, maxCols).Select(j => new string('─', colWidths[j] + 2))) + "┘");
        return sb.ToString();
    }

    private static List<TextSegment> ProcessInlineFormatting(string text)
    {
        var segments = new List<TextSegment>();
        var current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = false, IsCode = false };

        var i = 0;
        while (i < text.Length)
        {
            // Images ![alt](url)
            if (i < text.Length - 1 && text[i] == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                if (current.Text.Length > 0)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = false, IsCode = false };
                }

                var altStart = i + 2;
                var closeBracket = text.IndexOf(']', altStart);
                if (closeBracket > 0 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    var altText = text.Substring(altStart, closeBracket - altStart);
                    var closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > 0)
                    {
                        segments.Add(new TextSegment { Text = $"🖼 [{altText}]", IsBold = false, IsItalic = true, IsCode = false });
                        i = closeParen + 1;
                        continue;
                    }
                }
            }

            // Links [text](url)
            if (i < text.Length - 1 && text[i] == '[')
            {
                if (current.Text.Length > 0)
                {
                    segments.Add(current);
                    current = new TextSegment { Text = string.Empty, IsBold = false, IsItalic = false, IsCode = false };
                }

                var closeBracket = text.IndexOf(']', i + 1);
                if (closeBracket > 0 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    var linkText = text.Substring(i + 1, closeBracket - i - 1);
                    var closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > 0)
                    {
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
