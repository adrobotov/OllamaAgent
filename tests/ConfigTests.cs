using OllamaAgent.Models;

namespace OllamaAgent.Tests.Models;

public class ConfigTests
{
    [Fact]
    public void Config_DefaultValues_SetsExpectedDefaults()
    {
        // Arrange & Act
        var config = new Config();

        // Assert
        Assert.Equal("http://localhost:11434", config.BaseUrl);
        Assert.Equal("llama3.2", config.Model);
        Assert.Equal("system", config.Theme);
        Assert.Equal(20, config.ContextLimit);
        Assert.Equal(1200, config.WindowWidth);
        Assert.Equal(800, config.WindowHeight);
        Assert.Equal(string.Empty, config.WeatherApiKey);
        Assert.Equal(string.Empty, config.ChatFile);
    }

    [Fact]
    public void Config_IsValidBaseUrl_ReturnsTrue_ForValidUrls()
    {
        // Arrange & Act & Assert
        var config1 = new Config { BaseUrl = "http://localhost:11434" };
        var config2 = new Config { BaseUrl = "http://127.0.0.1:11434" };
        var config3 = new Config { BaseUrl = "https://example.com:8080" };
        var config4 = new Config { BaseUrl = "http://ollama-server:11434" };

        Assert.True(config1.IsValidBaseUrl);
        Assert.True(config2.IsValidBaseUrl);
        Assert.True(config3.IsValidBaseUrl);
        Assert.True(config4.IsValidBaseUrl);
    }

    [Fact]
    public void Config_IsValidBaseUrl_ReturnsFalse_ForInvalidUrls()
    {
        // Arrange & Act & Assert
        var config1 = new Config { BaseUrl = "" };
        var config2 = new Config { BaseUrl = "not-a-url" };
        var config3 = new Config { BaseUrl = "ftp://invalid:11434" };
        var config4 = new Config { BaseUrl = "   " };

        Assert.False(config1.IsValidBaseUrl);
        Assert.False(config2.IsValidBaseUrl);
        Assert.False(config3.IsValidBaseUrl);
        Assert.False(config4.IsValidBaseUrl);
    }
}
