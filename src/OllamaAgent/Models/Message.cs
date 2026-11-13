using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaAgent.Models;

/// <summary>
/// Represents a single chat message in the conversation
/// </summary>
public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Represents a tool call from the AI model (matches Ollama API schema)
/// </summary>
public class ToolCall
{
    [JsonPropertyName("function")]
    public FunctionCall Function { get; set; } = new();
}

/// <summary>
/// Represents a function call within a tool call
/// </summary>
public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public object? Arguments { get; set; }
}
