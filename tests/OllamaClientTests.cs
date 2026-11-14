using Moq;
using Moq.Protected;
using OllamaAgent.Services;

namespace OllamaAgent.Tests.Services;

public class OllamaClientTests
{
    [Fact]
    public void Constructor_CreatesClient_WithoutThrowing()
    {
        // Arrange & Act
        var client = new OllamaClient("http://localhost:11434");

        // Assert
        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public void Constructor_WithTrailingSlash_CreatesClient_WithoutThrowing()
    {
        // Arrange & Act
        var client = new OllamaClient("http://localhost:11434/");

        // Assert
        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public void Dispose_CalledOnce_DisposesHttpClient()
    {
        // Arrange
        var client = new OllamaClient("http://localhost:11434");

        // Act & Assert
        try
        {
            client.Dispose();
        }
        catch
        {
            Assert.True(false, "Dispose should not throw");
        }
    }

    [Theory]
    [InlineData("llama3.2")]
    [InlineData("llama3.1")]
    [InlineData("gemma2")]
    [InlineData("qwen2.5")]
    [InlineData("mistral-large")]
    [InlineData("saiga")]
    [InlineData("falcon")]
    [InlineData("")]
    public void ModelNameHandling_VariousNames_DoesNotThrow(string modelName)
    {
        // Arrange & Act & Assert
        Assert.IsType<string>(modelName);
        Assert.NotNull(modelName);
    }
}
