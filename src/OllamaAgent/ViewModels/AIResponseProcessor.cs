using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Services;
using OllamaAgent.Tools;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Processes AI responses from Ollama, including streaming, tool calling, and multi-turn loops.
/// </summary>
public partial class AIResponseProcessor
{
    private readonly OllamaClient _ollamaClient;
    private readonly ToolExecutor _toolExecutor;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly ChatMessageManager _messageManager;
    private readonly ToolManager _toolManager;

    public AIResponseProcessor(
        OllamaClient ollamaClient,
        ToolExecutor toolExecutor,
        Config config,
        ILogger logger,
        ChatMessageManager messageManager,
        ToolManager toolManager)
    {
        _ollamaClient = ollamaClient;
        _toolExecutor = toolExecutor;
        _config = config;
        _logger = logger;
        _messageManager = messageManager;
        _toolManager = toolManager;
    }

    /// <summary>
    /// Main entry point for sending a message and processing the AI response.
    /// </summary>
    public async Task SendMessageAsync(
        string userText,
        CancellationToken cancellationToken,
        Action<string> setStatus,
        Action<bool> setIsProcessing,
        Action<bool> setShowTypingIndicator,
        Action<string> setSessionName,
        Action<string> setStatusTextForImport,
        List<string> attachedImages)
    {
        _messageManager.AddUserMessage(userText, attachedImages);

        // Clear attached images after adding
        attachedImages.Clear();

        await ProcessAIResponseAsync(userText, cancellationToken, setStatus, setIsProcessing, setShowTypingIndicator, setSessionName);
    }

    /// <summary>
    /// Process AI response with multi-turn tool calling loop
    /// </summary>
    private async Task ProcessAIResponseAsync(
        string userText,
        CancellationToken cancellationToken,
        Action<string> setStatus,
        Action<bool> setIsProcessing,
        Action<bool> setShowTypingIndicator,
        Action<string> setSessionName)
    {
        setIsProcessing(true);
        setShowTypingIndicator(true);
        setStatus("Думаю...");

        try
        {
            var messages = BuildMessageList();
            var useTools = ShouldUseTools();
            var maxTurns = 10;

            for (var turn = 0; turn < maxTurns; turn++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = BuildChatRequest(messages, useTools);
                var (fullText, toolCalls) = await StreamResponseAsync(request, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                _logger.Log($"Response: {fullText?.Length ?? 0} chars, {toolCalls.Count} tool calls");

                // Display AI response
                if (!string.IsNullOrEmpty(fullText))
                {
                    _messageManager.AddAssistantMessage(fullText);
                }

                // Execute tool calls if any
                if (toolCalls.Count > 0)
                {
                    setStatus("Проверяю инструменты...");
                    await _toolManager.ExecuteToolCallsAsync(toolCalls, messages, _messageManager, _config.Model, setStatus, cancellationToken);
                    setStatus("Думаю...");
                    continue;
                }

                // Final response - already added via AddAssistantMessage
                break;
            }

            setStatus("Готово");
        }
        catch (OperationCanceledException)
        {
            setStatus("Отменено");
            _messageManager.AddErrorMessage("⏹ Запрос отменён");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing response: {ex.Message}");
            setStatus($"Ошибка: {ex.Message}");
            _messageManager.AddErrorMessage($"❌ Ошибка: {ex.Message}");
        }
        finally
        {
            setShowTypingIndicator(false);
            setIsProcessing(false);
        }
    }

    /// <summary>
    /// Build the message list with system prompt and history
    /// </summary>
    private List<Message> BuildMessageList()
    {
        var messages = new List<Message>();

        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            messages.Add(new Message { Role = "system", Content = _config.SystemPrompt });
        }

        var history = _messageManager.ConversationHistory.Count > _config.ContextLimit
            ? _messageManager.ConversationHistory.Skip(_messageManager.ConversationHistory.Count - _config.ContextLimit)
            : _messageManager.ConversationHistory;
        messages.AddRange(history);

        return messages;
    }

    /// <summary>
    /// Determine whether to use tools for the current model
    /// </summary>
    private bool ShouldUseTools()
    {
        return _toolExecutor.GetTools().Count > 0 && ModelSupportsTools(_config.Model);
    }

    /// <summary>
    /// Build a chat request for the current turn
    /// </summary>
    private ChatRequest BuildChatRequest(List<Message> messages, bool useTools)
    {
        var request = new ChatRequest
        {
            Model = _config.Model,
            Messages = messages,
            Stream = true
        };

        // Thinking mode
        if (_config.ThinkingEnabled && ModelSupportsThinking(_config.Model))
        {
            request.Think = _config.ThinkingDepth;
            _logger.Log($"Thinking mode enabled: {_config.ThinkingDepth}");
        }
        else if (_config.ThinkingEnabled && !ModelSupportsThinking(_config.Model))
        {
            _logger.Log($"Thinking not supported by: {_config.Model}");
        }

        // Tools
        if (useTools)
        {
            request.Tools = _toolExecutor.GetTools().Select(t => (object)t).ToList();
            var toolNames = _toolExecutor.GetTools().Select(t => t.Function.Name).ToList();
            _logger.Log($"Sending {toolNames.Count} tools: [{string.Join(", ", toolNames)}]");
        }

        return request;
    }

    /// <summary>
    /// Stream response from Ollama and extract text and tool calls
    /// </summary>
    private async Task<(string? text, List<ToolCall> toolCalls)> StreamResponseAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var response = await _ollamaClient.ChatStreamAsync(request, cancellationToken);

        var fullText = string.Empty;
        var toolCalls = new List<ToolCall>();
        var doneThinking = false;

        using var reader = new StreamReader(response);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var chunk = JsonSerializer.Deserialize<OllamaChunk>(line);
                if (chunk == null) continue;

                // Debug log
                if (!string.IsNullOrEmpty(chunk.Message.Content) || chunk.Message.ToolCalls?.Count > 0)
                {
                    _logger.Log($"📨 Chunk: Content={chunk.Message.Content?.Length ?? 0} chars, ToolCalls={chunk.Message.ToolCalls?.Count ?? 0}, Done={chunk.Done}");
                }

                // Accumulate text
                if (!string.IsNullOrEmpty(chunk.Message.Content))
                {
                    fullText += chunk.Message.Content;
                    if (!doneThinking)
                    {
                        doneThinking = true;
                    }
                }

                // Collect tool calls — merge by function name since they stream incrementally
                if (chunk.Message.ToolCalls?.Count > 0)
                {
                    foreach (var tc in chunk.Message.ToolCalls)
                    {
                        if (string.IsNullOrEmpty(tc.Function.Name)) continue;

                        var existing = toolCalls.FirstOrDefault(x => x.Function.Name == tc.Function.Name);
                        if (existing != null)
                        {
                            // Accumulate arguments (they stream incrementally)
                            if (tc.Function.Arguments is string newArgs && !string.IsNullOrEmpty(newArgs))
                            {
                                if (existing.Function.Arguments is string existingArgs)
                                    existing.Function.Arguments = existingArgs + newArgs;
                                else
                                    existing.Function.Arguments = newArgs;
                            }
                        }
                        else
                        {
                            toolCalls.Add(tc);
                        }
                    }
                }

                if (chunk.Done) break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing chunk: {ex.Message}");
            }
        }

        return (string.IsNullOrEmpty(fullText) ? null : fullText, toolCalls);
    }

    /// <summary>
    /// Check if a model supports tool calling based on its name/family
    /// </summary>
    private static bool ModelSupportsTools(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;

        var nameLower = modelName.ToLower();

        // Reasoning models DON'T support tool calling
        var reasoningModels = new[] { "deepseek-r1", "qwq", "r1", "qwq-32b" };
        foreach (var name in reasoningModels)
        {
            if (nameLower.Contains(name)) return false;
        }

        // Models known to support tools (specific versions only)
        var toolSupportingFamilies = new[] { "llama", "gemma", "mistral", "mixtral", "qwen", "codellama", "codegemma" };
        var toolSupportingNames = new[] { "llama3.1", "llama3.2", "gemma2", "gemma3", "gemma4", "mistral-large", "qwen2.5", "qwen3" };

        foreach (var family in toolSupportingFamilies)
        {
            if (nameLower.Contains(family)) return true;
        }

        foreach (var name in toolSupportingNames)
        {
            if (nameLower.Contains(name)) return true;
        }

        // Models known NOT to support tools
        var noToolNames = new[] { "saiga", "falcon", "stable-code", "tinyllama", "deepseek", "qwq" };
        foreach (var name in noToolNames)
        {
            if (nameLower.Contains(name)) return false;
        }

        // Default: assume no tool support for unknown models
        return false;
    }

    /// <summary>
    /// Check if a model supports thinking/reasoning mode
    /// </summary>
    private static bool ModelSupportsThinking(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;

        var nameLower = modelName.ToLower();

        // Only specific models support thinking
        var thinkingModels = new[] { "deepseek-r1", "qwq", "qwq-32b" };
        foreach (var name in thinkingModels)
        {
            if (nameLower.Contains(name)) return true;
        }

        return false;
    }
}
