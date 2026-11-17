using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OllamaAgent.Helpers;
using OllamaAgent.Models;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Manages chat messages collection and conversation history.
/// </summary>
public partial class ChatMessageManager
{
    /// <summary>
    /// UI-bound collection of chat message view models
    /// </summary>
    public ObservableCollection<ChatMessageViewModel> ChatMessages { get; } = new();

    /// <summary>
    /// List of messages for API conversation history
    /// </summary>
    public List<Message> ConversationHistory { get; } = new();

    /// <summary>
    /// Search text for filtering chat messages
    /// </summary>
    public string ChatSearchText { get; set; } = string.Empty;

    /// <summary>
    /// Filtered chat messages based on search text (uses fuzzy matching)
    /// </summary>
    public IEnumerable<ChatMessageViewModel> FilteredChatMessages =>
        string.IsNullOrWhiteSpace(ChatSearchText)
            ? ChatMessages
            : ChatMessages.Where(m =>
                FuzzySearch.Contains(m.Content, ChatSearchText) ||
                FuzzySearch.Contains(m.Sender, ChatSearchText));

    /// <summary>
    /// Add a user message to both UI and conversation history
    /// </summary>
    public void AddUserMessage(string userMessage, List<string>? images = null)
    {
        var displayText = string.IsNullOrEmpty(userMessage)
            ? $"📷 Изображение ({images?.Count ?? 0})"
            : userMessage;
        var messageViewModel = new ChatMessageViewModel(displayText, ChatMessageViewModel.MessageType.User)
        {
            Images = images
        };
        ChatMessages.Add(messageViewModel);

        var message = new Message { Role = "user", Content = userMessage };
        if (images != null && images.Count > 0)
        {
            message.Images = new List<string>(images);
        }
        ConversationHistory.Add(message);
    }

    /// <summary>
    /// Add an assistant message to UI and conversation history
    /// </summary>
    public void AddAssistantMessage(string content)
    {
        ChatMessages.Add(new ChatMessageViewModel(content, ChatMessageViewModel.MessageType.Assistant));
        ConversationHistory.Add(new Message { Role = "assistant", Content = content });
    }

    /// <summary>
    /// Add a tool result message to UI (not to conversation history)
    /// </summary>
    public void AddToolResultMessage(string content)
    {
        ChatMessages.Add(new ChatMessageViewModel(content, ChatMessageViewModel.MessageType.ToolResult));
    }

    /// <summary>
    /// Add an error message to UI
    /// </summary>
    public void AddErrorMessage(string content)
    {
        ChatMessages.Add(new ChatMessageViewModel(content, ChatMessageViewModel.MessageType.Error));
    }

    /// <summary>
    /// Clear all messages from UI and conversation history
    /// </summary>
    public void ClearChat()
    {
        ChatMessages.Clear();
        ConversationHistory.Clear();
    }
}
