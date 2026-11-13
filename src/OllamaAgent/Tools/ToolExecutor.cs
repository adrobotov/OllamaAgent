using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaAgent.Models;

namespace OllamaAgent.Tools;

/// <summary>
/// Result of tool execution
/// </summary>
public class ToolResult
{
    public string ToolCallId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsError { get; set; }
}

/// <summary>
/// Tool executor function signature
/// </summary>
public delegate Task<ToolResult> ToolFunction(Dictionary<string, object> args);

/// <summary>
/// Executes tool calls and returns results
/// </summary>
public class ToolExecutor
{
    private readonly Dictionary<string, (ToolDefinition Tool, ToolFunction Function)> _tools = new();

    /// <summary>
    /// Register a tool with its schema and implementation
    /// </summary>
    public void Register(string name, string description, Dictionary<string, object> parameters, ToolFunction function)
    {
        var tool = new ToolDefinition
        {
            Function = new FunctionTool
            {
                Name = name,
                Description = description,
                Parameters = parameters
            }
        };

        _tools[name] = (tool, function);
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    public List<ToolDefinition> GetTools()
    {
        return _tools.Values.Select(v => v.Tool).ToList();
    }

    /// <summary>
    /// Execute a tool call
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(ToolCall toolCall)
    {
        if (!_tools.TryGetValue(toolCall.Function.Name, out var def))
        {
            return new ToolResult
            {
                ToolCallId = string.Empty,
                Content = $"Error: unknown tool: {toolCall.Function.Name}",
                IsError = true
            };
        }

        try
        {
            var args = toolCall.Function.Arguments switch
            {
                Dictionary<string, object?> nullableDict => nullableDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)(kvp.Value ?? string.Empty)),
                JsonElement element => JsonToDictionary(element).ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)(kvp.Value ?? string.Empty)),
                string str => JsonSerializer.Deserialize<Dictionary<string, object>>(str) ?? new(),
                _ => new Dictionary<string, object>()
            };
            var result = await def.Function(args);
            return result;
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                ToolCallId = string.Empty,
                Content = $"Error: {ex.Message}",
                IsError = true
            };
        }
    }

    /// <summary>
    /// Convert JsonElement to Dictionary<string, object> recursively
    /// </summary>
    private static Dictionary<string, object?> JsonToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = JsonToValue(prop.Value);
        }
        return dict;
    }

    /// <summary>
    /// Convert JsonElement to appropriate C# type
    /// </summary>
    private static object? JsonToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonToValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? (object?)i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}
