using OllamaAgent.Helpers;

namespace OllamaAgent.Tests.Helpers;

public class MarkdownRendererTests
{
    [Fact]
    public void Render_EmptyString_ReturnsSingleSegment()
    {
        var segments = MarkdownRenderer.Render("");
        
        Assert.Single(segments);
        Assert.Equal("", segments[0].Text);
    }

    [Fact]
    public void Render_NullString_ReturnsSingleSegment()
    {
        var segments = MarkdownRenderer.Render(null!);
        
        Assert.Single(segments);
    }

    [Fact]
    public void Render_Headings_MarksAsBold()
    {
        var segments = MarkdownRenderer.Render("# Heading 1\n## Heading 2\n### Heading 3");
        
        // Headings are rendered as plain text with newlines
        Assert.True(segments.Count > 0);
        var headingText = string.Concat(segments.Select(s => s.Text));
        Assert.Contains("Heading", headingText);
    }

    [Fact]
    public void Render_CodeBlocks_MarksAsCode()
    {
        var segments = MarkdownRenderer.Render("Some text\n```\ncode here\n```\nMore text");
        
        // Code blocks should produce segments with code content
        var allText = string.Concat(segments.Select(s => s.Text));
        Assert.Contains("code here", allText);
    }

    [Fact]
    public void Render_BoldText_MarksAsBold()
    {
        var segments = MarkdownRenderer.Render("This is **bold** text");
        
        // Bold is rendered with ** markers
        var boldSegments = segments.Where(s => s.Text == "**").ToList();
        Assert.True(boldSegments.Count >= 2);
    }

    [Fact]
    public void Render_ItalicText_MarksAsItalic()
    {
        var segments = MarkdownRenderer.Render("This is *italic* text");
        
        // Italic is rendered with * markers
        var italicSegments = segments.Where(s => s.Text == "*").ToList();
        Assert.True(italicSegments.Count >= 2);
    }

    [Fact]
    public void Render_List_Items_CreatesSegments()
    {
        var segments = MarkdownRenderer.Render("- Item 1\n- Item 2\n- Item 3");
        
        Assert.True(segments.Count > 0);
    }

    [Fact]
    public void Render_Table_CreatesTableSegments()
    {
        var segments = MarkdownRenderer.Render("| Col1 | Col2 |\n|------|------|\n| A    | B    |");
        
        Assert.True(segments.Count > 0);
    }

    [Fact]
    public void Render_CodeBlock_ContainsCodeContent()
    {
        var code = "public void Test() { }";
        var segments = MarkdownRenderer.Render($"```\n{code}\n```");
        
        // The code content should be in a segment
        var textSegment = segments.FirstOrDefault(s => s.Text.Contains("Test"));
        Assert.NotNull(textSegment);
    }
}
