using OllamaAgent.Models;
using System.Text.Json;

namespace OllamaAgent.Tests;

public class ConfigTests
{
    [Fact]
    public void Config_DefaultValues_AreCorrect()
    {
        var config = new Config();
        
        Assert.Equal("http://localhost:11434", config.BaseUrl);
        Assert.Equal("llama3.2", config.Model);
        Assert.Equal("system", config.Theme);
        Assert.False(config.ThinkingEnabled);
        Assert.Equal("medium", config.ThinkingDepth);
    }

    [Fact]
    public void Config_IsValidBaseUrl_ValidUrl_ReturnsTrue()
    {
        var config = new Config { BaseUrl = "http://localhost:11434" };
        Assert.True(config.IsValidBaseUrl);
    }

    [Fact]
    public void Config_IsValidBaseUrl_HttpsUrl_ReturnsTrue()
    {
        var config = new Config { BaseUrl = "https://ollama.example.com" };
        Assert.True(config.IsValidBaseUrl);
    }

    [Fact]
    public void Config_IsValidBaseUrl_InvalidUrl_ReturnsFalse()
    {
        var config = new Config { BaseUrl = "not-a-url" };
        Assert.False(config.IsValidBaseUrl);
    }

    [Fact]
    public void Config_Serialize_Deserialize_RoundTrip()
    {
        var original = new Config
        {
            BaseUrl = "http://test:8080",
            Model = "llama3.1",
            Theme = "dark",
            ThinkingEnabled = true,
            ThinkingDepth = "high",
            WeatherApiKey = "test-key"
        };

        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        var deserialized = JsonSerializer.Deserialize<Config>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal(original.BaseUrl, deserialized!.BaseUrl);
        Assert.Equal(original.Model, deserialized.Model);
        Assert.Equal(original.Theme, deserialized.Theme);
        Assert.Equal(original.ThinkingEnabled, deserialized.ThinkingEnabled);
        Assert.Equal(original.ThinkingDepth, deserialized.ThinkingDepth);
    }

    [Fact]
    public void Config_Serialize_ExcludesNullFields()
    {
        var config = new Config();
        
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull 
        });
        
        // WeatherApiKey should not appear if empty
        Assert.DoesNotContain("weatherApiKey", json);
    }

    [Fact]
    public void Config_Deserialize_MissingFields_UsesDefaults()
    {
        var json = "{}";
        
        var config = JsonSerializer.Deserialize<Config>(json);
        
        Assert.NotNull(config);
        Assert.Equal("http://localhost:11434", config!.BaseUrl);
        Assert.Equal("llama3.2", config.Model);
    }

    [Fact]
    public void Config_AutoUpdateEnabled_DefaultsToTrue()
    {
        var config = new Config();
        Assert.True(config.AutoUpdateEnabled);
    }
}
