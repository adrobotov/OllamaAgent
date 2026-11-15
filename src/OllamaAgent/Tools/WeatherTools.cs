using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Weather API client for weatherapi.com
/// Get free API key: https://www.weatherapi.com/my/
/// </summary>
public class WeatherClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "http://api.weatherapi.com/v1/current.json";
    private bool _disposed;

    public WeatherClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Get current weather for a location
    /// </summary>
    public async Task<string> GetCurrentWeatherAsync(string location)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException(
                "Weather API key not configured. Set weather_api_key in config.json. " +
                "Get a free key at https://www.weatherapi.com/my/");

        var url = $"{BaseUrl}?key={Uri.EscapeDataString(_apiKey)}&q={Uri.EscapeDataString(location)}";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Weather API error ({response.StatusCode}): {errorContent}");
        }

        var weather = await response.Content.ReadFromJsonAsync<WeatherResponse>();
        if (weather == null)
            throw new Exception("Invalid response from weather API");

        return FormatWeather(weather);
    }

    private static string FormatWeather(WeatherResponse w)
    {
        var loc = w.Location;
        var cur = w.Current;
        var cond = cur.Condition;

        // Translate wind direction to Russian
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

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}

// ===== API Response Models =====
// weatherapi.com returns: {"location": {...}, "current": {...}}

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

/// <summary>
/// Weather tools registration
/// </summary>
public static class WeatherTools
{
    public static void Register(ToolExecutor executor, WeatherClient client)
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
                    return new ToolResult
                    {
                        Content = "Error: missing required argument: location",
                        IsError = true
                    };

                try
                {
                    var weather = await client.GetCurrentWeatherAsync(location);
                    return new ToolResult { Content = weather };
                }
                catch (InvalidOperationException ex)
                {
                    return new ToolResult
                    {
                        Content = $"⚠️ {ex.Message}",
                        IsError = true
                    };
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
}
