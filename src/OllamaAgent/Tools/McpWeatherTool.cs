using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Weather tool with MCP support and direct HTTP fallback
/// </summary>
public static class McpWeatherTool
{
    private static McpClient? _mcpClient;
    private static string? _apiKey;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialize the weather tool (try MCP first, fallback to direct HTTP)
    /// </summary>
    public static async Task InitializeAsync(string apiKey)
    {
        _apiKey = apiKey;

        // Try MCP first
        try
        {
            var mcpClient = new McpClient();
            await mcpClient.InitializeAsync(
                command: "npx",
                args: new[] { "-y", "weatherapi-mcp" },
                env: new Dictionary<string, string>
                {
                    ["WEATHERAPI_KEY"] = apiKey
                }
            );

            var tools = await mcpClient.ListToolsAsync();
            AppLogger.Logger.Log($"🔧 MCP tools available: {tools.Count}");
            foreach (var tool in tools)
            {
                AppLogger.Logger.Log($"  - {tool.Name}: {tool.Description}");
            }

            lock (_lock)
            {
                _mcpClient = mcpClient;
            }

            AppLogger.Logger.Log("🌤️ Weather tool using MCP server");
            return;
        }
        catch (Exception ex)
        {
            AppLogger.Logger.LogWarning($"⚠️ MCP weather server unavailable ({ex.GetType().Name}), using direct HTTP fallback");
        }

        AppLogger.Logger.Log("🌤️ Weather tool using direct HTTP (weatherapi.com)");
    }

    /// <summary>
    /// Register the weather tool with the executor
    /// </summary>
    public static void Register(ToolExecutor executor)
    {
        executor.Register(
            "get_weather",
            "Get current weather conditions for a location (city name or coordinates).",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "City name or coordinates (e.g., 'Moscow', 'London', '40.7128,-74.0060')"
                    }
                },
                ["required"] = new[] { "location" }
            },
            async args =>
            {
                if (!args.TryGetValue("location", out var locObj) || locObj is not string location)
                {
                    return new ToolResult
                    {
                        Content = "Error: missing required argument: location",
                        IsError = true
                    };
                }

                // Get API key
                string? key;
                lock (_lock)
                {
                    key = _apiKey;
                }

                if (string.IsNullOrEmpty(key))
                {
                    return new ToolResult
                    {
                        Content = "❌ Weather service not configured. Set weather_api_key in config.json",
                        IsError = true
                    };
                }

                try
                {
                    // Try MCP first
                    McpClient? mcp;
                    lock (_lock)
                    {
                        mcp = _mcpClient;
                    }

                    if (mcp != null)
                    {
                        try
                        {
                            var result = await mcp.CallToolAsync(
                                "get_weather",
                                new Dictionary<string, object> { ["location"] = location }
                            );

                            if (result.TryGetProperty("content", out var contentArray) &&
                                contentArray.GetArrayLength() > 0)
                            {
                                var text = contentArray[0].GetProperty("text").GetString() ?? string.Empty;
                                return new ToolResult { Content = text };
                            }

                            return new ToolResult
                            {
                                Content = "No weather data returned from MCP server",
                                IsError = true
                            };
                        }
                        catch
                        {
                            AppLogger.Logger.Log("🔄 MCP call failed, falling back to direct HTTP");
                        }
                    }

                    // Fallback to direct HTTP
                    return await DirectWeatherClient.FetchAsync(key, location);
                }
                catch (Exception ex)
                {
                    return new ToolResult
                    {
                        Content = $"❌ Error fetching weather: {ex.Message}",
                        IsError = true
                    };
                }
            });
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public static void Dispose()
    {
        McpClient? mcpClient;
        lock (_lock)
        {
            mcpClient = _mcpClient;
            _mcpClient = null;
            _apiKey = null;
        }
        mcpClient?.Dispose();
    }
}

// ===== Direct HTTP Weather Client =====

public static class DirectWeatherClient
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<ToolResult> FetchAsync(string apiKey, string location)
    {
        if (string.IsNullOrEmpty(apiKey))
            return new ToolResult
            {
                Content = "Weather API key not configured",
                IsError = true
            };

        var url = $"http://api.weatherapi.com/v1/current.json?key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(location)}";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return new ToolResult
            {
                Content = $"Weather API error ({response.StatusCode}): {errorContent}",
                IsError = true
            };
        }

        var weather = await response.Content.ReadFromJsonAsync<WeatherResponse>();
        if (weather == null)
            return new ToolResult
            {
                Content = "Invalid response from weather API",
                IsError = true
            };

        var formatted = FormatWeather(weather);
        return new ToolResult { Content = formatted };
    }

    private static string FormatWeather(WeatherResponse w)
    {
        var loc = w.Location;
        var cur = w.Current;
        var cond = cur.Condition;
        var windDirRu = TranslateWindDir(cur.WindDir);

        return $"📍 {loc.Name}, {loc.Country}\n" +
               $"🕐 Местное время: {loc.Localtime}\n\n" +
               $"🌡️  Температура: {cur.TempC:F1}°C ({cur.TempF:F1}°F)\n" +
               $"🌡️  Ощущается как: {cur.FeelsLikeC:F1}°C\n" +
               $"🌤️  {cond.Text}\n" +
               $"💨 Ветер: {cur.WindKph:F1} км/ч {windDirRu}\n" +
               $"💧 Влажность: {cur.Humidity}%\n" +
               $"🌧️  Осадки: {cur.PrecipMm:F1} мм";
    }

    private static string TranslateWindDir(string dir)
    {
        return dir.ToUpperInvariant() switch
        {
            "N" => "(с С)",
            "NNE" => "(с С-СВ)",
            "NE" => "(с СВ)",
            "ENE" => "(с В-СВ)",
            "E" => "(с В)",
            "ESE" => "(с В-ЮВ)",
            "SE" => "(с ЮВ)",
            "SSE" => "(с Ю-ЮВ)",
            "S" => "(с Ю)",
            "SSW" => "(с Ю-ЮЗ)",
            "SW" => "(с ЮЗ)",
            "WSW" => "(с З-ЮЗ)",
            "W" => "(с З)",
            "WNW" => "(с З-СЗ)",
            "NW" => "(с СЗ)",
            "NNW" => "(с С-СЗ)",
            _ => $"({dir})"
        };
    }
}

// ===== API Response Models =====

public class WeatherResponse
{
    [JsonPropertyName("location")]
    public LocationInfo Location { get; set; } = new();

    [JsonPropertyName("current")]
    public CurrentWeather Current { get; set; } = new();
}

public class LocationInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("localtime")]
    public string Localtime { get; set; } = string.Empty;
}

public class CurrentWeather
{
    [JsonPropertyName("temp_c")]
    public double TempC { get; set; }

    [JsonPropertyName("temp_f")]
    public double TempF { get; set; }

    [JsonPropertyName("feelslike_c")]
    public double FeelsLikeC { get; set; }

    [JsonPropertyName("wind_kph")]
    public double WindKph { get; set; }

    [JsonPropertyName("wind_dir")]
    public string WindDir { get; set; } = string.Empty;

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("precip_mm")]
    public double PrecipMm { get; set; }

    [JsonPropertyName("condition")]
    public Condition Condition { get; set; } = new();
}

public class Condition
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
