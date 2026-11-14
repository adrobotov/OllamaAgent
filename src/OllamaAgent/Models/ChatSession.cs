using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaAgent.Models;

/// <summary>
/// Represents a saved chat session
/// </summary>
public class ChatSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Whether this session is currently selected
    /// </summary>
    [JsonIgnore]
    public bool IsSelected { get; set; }
}

/// <summary>
/// Serializable chat message for persistence
/// </summary>
public class ChatMessage
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
    public List<ChatToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Serializable tool call for persistence
/// </summary>
public class ChatToolCall
{
    [JsonPropertyName("function")]
    public ChatFunctionCall Function { get; set; } = new();
}

/// <summary>
/// Serializable function call for persistence
/// </summary>
public class ChatFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public object? Arguments { get; set; }
}
