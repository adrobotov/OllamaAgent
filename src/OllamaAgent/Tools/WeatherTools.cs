using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Weather API client
/// </summary>
public class WeatherClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "http://api.weatherapi.com/v1";

    public WeatherClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> GetCurrentWeatherAsync(string location)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new Exception("Weather API key not configured. Set weather_api_key in config.json");

        var url = $"{_baseUrl}/current.json?key={_apiKey}&q={Uri.EscapeDataString(location)}";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {error}");
        }

        var weather = await response.Content.ReadFromJsonAsync<CurrentWeatherResponse>();
        return FormatCurrentWeather(weather!);
    }

    public async Task<string> GetForecastAsync(string location, int days = 3)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new Exception("Weather API key not configured. Set weather_api_key in config.json");

        days = Math.Max(1, Math.Min(3, days));
        
        var url = $"{_baseUrl}/forecast.json?key={_apiKey}&q={Uri.EscapeDataString(location)}&days={days}";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {error}");
        }

        var forecast = await response.Content.ReadFromJsonAsync<ForecastResponse>();
        return FormatForecast(forecast!);
    }

    private static string FormatCurrentWeather(CurrentWeatherResponse w)
    {
        return $"📍 {w.Location.Name}, {w.Location.Country} (local time: {w.Location.Localtime})\n" +
               $"🌡️  Temperature: {w.Current.TempC:F1}°C ({w.Current.TempF:F1}°F), feels like {w.Current.FeelsLikeC:F1}°C\n" +
               $"🌤️  Condition: {w.Current.Condition.Text}\n" +
               $"💨 Wind: {w.Current.WindKph:F1} km/h\n" +
               $"💧 Humidity: {w.Current.Humidity}%";
    }

    private static string FormatForecast(ForecastResponse f)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📍 {f.Location.Name}, {f.Location.Country}\n");

        foreach (var day in f.Forecast.Forecastday)
        {
            sb.AppendLine($"{day.Date}: {day.Day.MaxTempC:F1}°C / {day.Day.MinTempC:F1}°C, {day.Day.Condition.Text}, Wind: {day.Day.MaxWindKph:F1} km/h");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Weather API response models
/// </summary>
public class CurrentWeatherResponse
{
    public LocationInfo Location { get; set; } = new();
    public CurrentWeather Current { get; set; } = new();
}

public class ForecastResponse
{
    public LocationInfo Location { get; set; } = new();
    public ForecastData Forecast { get; set; } = new();
}

public class LocationInfo
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TzId { get; set; } = string.Empty;
    public string Localtime { get; set; } = string.Empty;
}

public class CurrentWeather
{
    public double TempC { get; set; }
    public double TempF { get; set; }
    public WeatherCondition Condition { get; set; } = new();
    public double WindKph { get; set; }
    public int Humidity { get; set; }
    public double FeelsLikeC { get; set; }
}

public class WeatherCondition
{
    public string Text { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class ForecastData
{
    public List<ForecastDay> Forecastday { get; set; } = new();
}

public class ForecastDay
{
    public string Date { get; set; } = string.Empty;
    public DayWeather Day { get; set; } = new();
}

public class DayWeather
{
    public double MaxTempC { get; set; }
    public double MinTempC { get; set; }
    public WeatherCondition Condition { get; set; } = new();
    public double MaxWindKph { get; set; }
}

/// <summary>
/// Weather-related tools
/// </summary>
public static class WeatherTools
{
    public static void Register(ToolExecutor executor, WeatherClient client)
    {
        // get_weather: Get current weather
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
                        ["description"] = "City name, city code, or coordinates (e.g., 'Moscow', 'London')"
                    }
                },
                ["required"] = new[] { "location" }
            },
            async args =>
            {
                if (!args.TryGetValue("location", out var locObj) || locObj is not string location)
                    return new ToolResult { Content = "Error: missing required argument: location", IsError = true };

                var weather = await client.GetCurrentWeatherAsync(location);
                return new ToolResult { Content = weather };
            });

        // get_forecast: Get weather forecast
        executor.Register(
            "get_forecast",
            "Get weather forecast for a location (1-3 days available).",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "City name, city code, or coordinates"
                    },
                    ["days"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Number of days forecast (1-3, default 3)"
                    }
                },
                ["required"] = new[] { "location" }
            },
            async args =>
            {
                if (!args.TryGetValue("location", out var locObj) || locObj is not string location)
                    return new ToolResult { Content = "Error: missing required argument: location", IsError = true };

                var days = 3;
                if (args.TryGetValue("days", out var daysObj) && daysObj is int d)
                    days = d;

                var forecast = await client.GetForecastAsync(location, days);
                return new ToolResult { Content = forecast };
            });
    }
}
