using System.Text.Json.Serialization;

namespace OllamaAgent.Models;

/// <summary>
/// Application configuration
/// </summary>
public class Config
{
    // Server settings
    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "llama3.2";

    // Chat settings
    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; set; } = "You are a helpful assistant with access to tools. When the user asks about information that requires tools (like weather, file operations, git operations, etc.), you MUST use the available tools to get the information. Always call the appropriate tool first, then provide a response based on the tool's result. Do not make up information that you cannot verify with tools.";

    [JsonPropertyName("context_limit")]
    public int ContextLimit { get; set; } = 20;

    [JsonPropertyName("chat_file")]
    public string ChatFile { get; set; } = string.Empty;

    // UI settings
    [JsonPropertyName("window_width")]
    public int WindowWidth { get; set; } = 1200;

    [JsonPropertyName("window_height")]
    public int WindowHeight { get; set; } = 800;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";

    // Tool API keys
    [JsonPropertyName("weather_api_key")]
    public string WeatherApiKey { get; set; } = string.Empty;
}
