using OllamaAgent.Helpers;

namespace OllamaAgent.Tests.Helpers;

public class FuzzySearchTests
{
    [Fact]
    public void Contains_ExactMatch_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("Hello World", "World"));
    }

    [Fact]
    public void Contains_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("Hello World", "world"));
        Assert.True(FuzzySearch.Contains("HELLO WORLD", "hello"));
    }

    [Fact]
    public void Contains_Substring_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("OllamaAgent is great", "Ollama"));
        Assert.True(FuzzySearch.Contains("OllamaAgent is great", "great"));
    }

    [Fact]
    public void Contains_FuzzyMatch_ReturnsTrue()
    {
        // Subsequence matching
        Assert.True(FuzzySearch.Contains("Hello World", "HW"));
        Assert.True(FuzzySearch.Contains("OllamaAgent", "OA"));
        Assert.True(FuzzySearch.Contains("Chat Session", "CS"));
    }

    [Fact]
    public void Contains_NotFound_ReturnsFalse()
    {
        Assert.False(FuzzySearch.Contains("Hello World", "xyz"));
        Assert.False(FuzzySearch.Contains("Hello World", "HWX"));
    }

    [Fact]
    public void Contains_EmptyQuery_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("Hello", ""));
    }

    [Fact]
    public void Contains_EmptyTextAndQuery_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("", ""));
    }

    [Fact]
    public void Contains_NullText_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains(null!, ""));
    }

    [Fact]
    public void Contains_FuzzyPartialMatch_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("OllamaAgent", "llamaAge"));
        Assert.True(FuzzySearch.Contains("TestMessage", "Tm"));
    }

    [Fact]
    public void Contains_ExactFullMatch_ReturnsTrue()
    {
        Assert.True(FuzzySearch.Contains("Hello World", "Hello World"));
    }
}
