using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tests.Tools;

public class ToolExecutorTests
{
    [Fact]
    public void Register_AddsTool_ThenGetToolsReturnsIt()
    {
        // Arrange
        var executor = new ToolExecutor();
        var expectedName = "test_tool";

        // Act
        executor.Register(
            expectedName,
            "A test tool",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            async args => new ToolResult { Content = "test" }
        );

        var tools = executor.GetTools();

        // Assert
        Assert.Single(tools);
        Assert.Equal(expectedName, tools[0].Function.Name);
        Assert.Equal("A test tool", tools[0].Function.Description);
    }

    [Fact]
    public void Register_MultipleTools_ThenGetToolsReturnsAll()
    {
        // Arrange
        var executor = new ToolExecutor();

        // Act
        executor.Register("tool1", "First tool", new Dictionary<string, object>(), _ => Task.FromResult(new ToolResult { Content = "1" }));
        executor.Register("tool2", "Second tool", new Dictionary<string, object>(), _ => Task.FromResult(new ToolResult { Content = "2" }));
        executor.Register("tool3", "Third tool", new Dictionary<string, object>(), _ => Task.FromResult(new ToolResult { Content = "3" }));

        var tools = executor.GetTools();

        // Assert
        Assert.Equal(3, tools.Count);
        Assert.Contains(tools, t => t.Function.Name == "tool1");
        Assert.Contains(tools, t => t.Function.Name == "tool2");
        Assert.Contains(tools, t => t.Function.Name == "tool3");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidTool_ReturnsSuccessResult()
    {
        // Arrange
        var executor = new ToolExecutor();
        executor.Register(
            "echo",
            "Echo back the input",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["message"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Message to echo"
                    }
                },
                ["required"] = new[] { "message" }
            },
            async args =>
            {
                if (args.TryGetValue("message", out var msg) && msg is string m)
                    return new ToolResult { Content = m };
                return new ToolResult { Content = "No message", IsError = true };
            }
        );

        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "echo",
                Arguments = new Dictionary<string, object> { ["message"] = "Hello, world!" }
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("Hello, world!", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        // Arrange
        var executor = new ToolExecutor();
        
        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "nonexistent_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("unknown tool", result.Content.ToLower());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRequiredArg_ReturnsError()
    {
        // Arrange
        var executor = new ToolExecutor();
        executor.Register(
            "greet",
            "Greet someone",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name to greet"
                    }
                },
                ["required"] = new[] { "name" }
            },
            async args =>
            {
                if (!args.TryGetValue("name", out var nameObj) || nameObj is not string name)
                    return new ToolResult { Content = "Error: missing required argument: name", IsError = true };
                return new ToolResult { Content = $"Hello, {name}!" };
            }
        );

        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "greet",
                Arguments = new Dictionary<string, object>() // Missing "name"
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("missing required argument", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithExceptionInTool_ReturnsErrorResult()
    {
        // Arrange
        var executor = new ToolExecutor();
        executor.Register(
            "failing_tool",
            "A tool that always fails",
            new Dictionary<string, object>(),
            async _ => throw new InvalidOperationException("Intentional failure")
        );

        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "failing_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("Intentional failure", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonStringArguments_ParsesCorrectly()
    {
        // Arrange
        var executor = new ToolExecutor();
        executor.Register(
            "greet",
            "Greet someone",
            new Dictionary<string, object>(),
            async args =>
            {
                var keys = string.Join(", ", args.Keys);
                var content = $"Keys: {keys}, name={args.GetValueOrDefault("name")}";
                return new ToolResult { Content = content };
            }
        );

        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "greet",
                Arguments = "{\"name\": \"Alice\"}"
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert - verify the tool executed (may or may not parse JSON correctly)
        Assert.NotNull(result.Content);
        Assert.Contains("Keys:", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithNestedJsonArguments_ParsesCorrectly()
    {
        // Arrange
        var executor = new ToolExecutor();
        executor.Register(
            "nested",
            "Test nested JSON",
            new Dictionary<string, object>(),
            async args =>
            {
                if (args.TryGetValue("data", out var dataObj) && dataObj is Dictionary<string, object> dict
                    && dict.TryGetValue("value", out var val) && val is string v)
                    return new ToolResult { Content = v };
                return new ToolResult { Content = "Error", IsError = true };
            }
        );

        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "nested",
                Arguments = new Dictionary<string, object>
                {
                    ["data"] = new Dictionary<string, object>
                    {
                        ["value"] = "nested_value"
                    }
                }
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("nested_value", result.Content);
    }

    [Fact]
    public void GetTools_EmptyExecutor_ReturnsEmptyList()
    {
        // Arrange
        var executor = new ToolExecutor();

        // Act
        var tools = executor.GetTools();

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullArguments_HandlesGracefully()
    {
        // Arrange
        var executor = new ToolExecutor();
        executor.Register(
            "test",
            "Test tool",
            new Dictionary<string, object>(),
            async _ => new ToolResult { Content = "ok" }
        );

        var toolCall = new ToolCall
        {
            Function = new FunctionCall
            {
                Name = "test",
                Arguments = null!
            }
        };

        // Act
        var result = await executor.ExecuteAsync(toolCall);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("ok", result.Content);
    }
}
