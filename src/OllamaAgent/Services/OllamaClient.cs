using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OllamaAgent.Models;

namespace OllamaAgent.Services;

/// <summary>
/// Ollama API client with streaming and tool calling support
/// </summary>
public class OllamaClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OllamaClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>
    /// List available models
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var modelList = JsonSerializer.Deserialize<ModelListResponse>(json);
        
        if (modelList == null) return new List<ModelInfo>();

        var models = new List<ModelInfo>();
        foreach (var apiModel in modelList.Models)
        {
            var family = apiModel.Details.Family;
            if (string.IsNullOrEmpty(family) && apiModel.Details.Families?.Count > 0)
            {
                family = apiModel.Details.Families[0];
            }

            var modelInfo = new ModelInfo
            {
                Name = apiModel.Name,
                Details = apiModel.Details,
                Capabilities = DetectCapabilities(apiModel.Name, family, apiModel.Details.ParameterSize, apiModel.Details.QuantizationLevel)
            };

            models.Add(modelInfo);
        }

        return models;
    }

    /// <summary>
    /// Send streaming chat request
    /// </summary>
    public async Task<Stream> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        Console.WriteLine($"[OllamaRequest] {json}");
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[OllamaError] {response.StatusCode}: {errorBody}");
            throw new HttpRequestException($"Ollama API error: {response.StatusCode} - {errorBody}");
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <summary>
    /// Send non-streaming chat request
    /// </summary>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        Console.WriteLine($"[OllamaRequest] {json}");
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[OllamaError] {response.StatusCode}: {errorBody}");
            throw new HttpRequestException($"Ollama API error: {response.StatusCode} - {errorBody}");
        }

        var jsonResult = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ChatResponse>(jsonResult) ?? new ChatResponse();
    }

    /// <summary>
    /// Detect model capabilities based on name and family
    /// </summary>
    private static List<string> DetectCapabilities(string name, string family, string paramSize, string quantization)
    {
        var caps = new List<string>();
        var nameLower = name.ToLower();
        var familyLower = family.ToLower();
        
        // Check if this is a reasoning model (these DON'T support tool calling)
        var isReasoningModel = nameLower.Contains("deepseek-r1") || nameLower.Contains("qwq") || 
                               nameLower.Contains("r1") || nameLower.Contains("qwq-32b");

        // Tool calling support (only for non-reasoning models)
        if (!isReasoningModel && (familyLower.Contains("llama") || familyLower.Contains("gemma") || familyLower.Contains("mistral") || familyLower.Contains("mixtral") || familyLower.Contains("qwen") || familyLower.Contains("codellama") || familyLower.Contains("codegemma") ||
            nameLower.Contains("llama3.1") || nameLower.Contains("llama3.2") ||
            nameLower.Contains("gemma2") || nameLower.Contains("gemma3") || nameLower.Contains("gemma4") ||
            nameLower.Contains("mistral-large") || nameLower.Contains("qwen2.5") || nameLower.Contains("qwen3")))
        {
            caps.Add("🔧 Tool Calling");
        }

        // Vision support
        if (nameLower.Contains("llava") || nameLower.Contains("llm-vision") || nameLower.Contains("bakllava") || nameLower.Contains("moondream"))
        {
            caps.Add("👁️ Vision");
        }

        // Coding support
        if (familyLower.Contains("codellama") || nameLower.Contains("codellama") || nameLower.Contains("deepseek-coder") || nameLower.Contains("codegemma"))
        {
            caps.Add("💻 Coding");
        }

        // Thinking/reasoning models
        if (nameLower.Contains("deepseek-r1") || nameLower.Contains("qwq") || nameLower.Contains("mistral-large"))
        {
            caps.Add("🧠 Reasoning");
        }

        // Small/fast models
        if (paramSize.Contains("M") || paramSize.Contains("0.4") || paramSize.Contains("1.5"))
        {
            caps.Add("⚡ Fast");
        }

        if (caps.Count == 0)
        {
            caps.Add("📝 Base");
        }

        return caps;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
