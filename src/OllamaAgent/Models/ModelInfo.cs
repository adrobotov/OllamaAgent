using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaAgent.Models;

/// <summary>
/// Model information with capabilities detection
/// </summary>
public class ModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public ModelDetails Details { get; set; } = new();

    /// <summary>
    /// Display name with capability badges
    /// </summary>
    public string DisplayName => $"{Name} [{string.Join(", ", Capabilities)}]";

    /// <summary>
    /// Detected capabilities
    /// </summary>
    [JsonIgnore]
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>
/// Model details from Ollama API
/// </summary>
public class ModelDetails
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("families")]
    public List<string>? Families { get; set; }

    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; } = string.Empty;

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; } = string.Empty;
}

/// <summary>
/// Model list response from Ollama API
/// </summary>
public class ModelListResponse
{
    [JsonPropertyName("models")]
    public List<ApiModel> Models { get; set; } = new();
}

/// <summary>
/// API model from Ollama /api/tags
/// </summary>
public class ApiModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public ModelDetails Details { get; set; } = new();
}
