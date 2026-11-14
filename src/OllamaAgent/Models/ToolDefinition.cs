using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaAgent.Models;

/// <summary>
/// Tool definition for Ollama API
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionTool Function { get; set; } = new();
}

/// <summary>
/// Function tool definition
/// </summary>
public class FunctionTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}
