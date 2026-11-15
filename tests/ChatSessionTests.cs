using OllamaAgent.Models;
using System.Text.Json;

namespace OllamaAgent.Tests;

public class ChatSessionTests
{
    [Fact]
    public void ChatSession_DefaultId_IsNotEmpty()
    {
        var session = new ChatSession();
        
        Assert.NotEmpty(session.Id);
    }

    [Fact]
    public void ChatSession_DefaultCreatedAt_IsUtc()
    {
        var before = DateTime.UtcNow;
        var session = new ChatSession();
        var after = DateTime.UtcNow;
        
        Assert.True(session.CreatedAt >= before && session.CreatedAt <= after);
    }

    [Fact]
    public void ChatSession_Serialize_Deserialize_RoundTrip()
    {
        var session = new ChatSession
        {
            Name = "Test Chat",
            Model = "llama3.2",
            Messages = 
            {
                new ChatMessage { Role = "user", Content = "Hello" },
                new ChatMessage { Role = "assistant", Content = "Hi there!" }
            }
        };

        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        var deserialized = JsonSerializer.Deserialize<ChatSession>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal(session.Name, deserialized!.Name);
        Assert.Equal(session.Model, deserialized.Model);
        Assert.Equal(session.Messages.Count, deserialized.Messages.Count);
        Assert.Equal("user", deserialized.Messages[0].Role);
        Assert.Equal("Hello", deserialized.Messages[0].Content);
    }

    [Fact]
    public void ChatSession_IsSelected_IsIgnoredBySerializer()
    {
        var session = new ChatSession { IsSelected = true };
        
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        // IsSelected should not be in the JSON
        Assert.DoesNotContain("isSelected", json);
    }

    [Fact]
    public void ChatSession_EmptyMessages_CanSerialize()
    {
        var session = new ChatSession { Name = "Empty" };
        
        var json = JsonSerializer.Serialize(session);
        var deserialized = JsonSerializer.Deserialize<ChatSession>(json);
        
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized!.Messages);
    }
}
