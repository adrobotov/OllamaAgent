using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaAgent.Models;

/// <summary>
/// Request to the Ollama chat API
/// </summary>
public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("tools")]
    public List<object>? Tools { get; set; }

    [JsonPropertyName("think")]
    public object? Think { get; set; }

    [JsonPropertyName("format")]
    public object? Format { get; set; }
}

/// <summary>
/// Streaming response chunk from Ollama
/// </summary>
public class OllamaChunk
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

/// <summary>
/// Full chat response from Ollama
/// </summary>
public class ChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}
