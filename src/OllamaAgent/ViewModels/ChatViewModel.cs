using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OllamaAgent.Models;
using OllamaAgent.Services;
using OllamaAgent.Tools;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Main chat view model with tool calling support
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private OllamaClient _ollamaClient;
    private ToolExecutor _toolExecutor;
    private Config _config;
    private readonly string _rootDir;
    private WebClient? _webClient;
    private WeatherClient? _weatherClient;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Готово";

    [ObservableProperty]
    private bool _isProcessing;

    /// <summary>
    /// Shows typing indicator when AI is processing
    /// </summary>
    [ObservableProperty]
    private bool _showTypingIndicator;

    [ObservableProperty]
    private bool _showSettings;

    [ObservableProperty]
    private string _selectedModelDisplay = string.Empty;

    [ObservableProperty]
    private string _baseUrl;

    [ObservableProperty]
    private string _systemPrompt;

    [ObservableProperty]
    private string _weatherApiKey;

    [ObservableProperty]
    private bool _thinkingEnabled;

    [ObservableProperty]
    private string _selectedThinkingDepth = "medium";

    private ObservableCollection<string> _thinkingDepthOptions = new(["low", "medium", "high"]);

    public ObservableCollection<string> ThinkingDepthOptions
    {
        get => _thinkingDepthOptions;
        set => SetProperty(ref _thinkingDepthOptions, value);
    }

    private ObservableCollection<string> _modelOptions = new();

    public ObservableCollection<string> ModelOptions
    {
        get => _modelOptions;
        set => SetProperty(ref _modelOptions, value);
    }

    public ObservableCollection<ChatMessageViewModel> ChatMessages { get; set; } = new();

    public List<Message> ConversationHistory { get; set; } = new();

    public List<ModelInfo> AvailableModels { get; set; } = new();

    /// <summary>
    /// List of chat sessions
    /// </summary>
    public ObservableCollection<ChatSession> ChatSessions { get; } = new();

    /// <summary>
    /// Currently active session
    /// </summary>
    public ChatSession? ActiveSession { get; private set; }

    /// <summary>
    /// Current session name for display
    /// </summary>
    [ObservableProperty]
    private string _sessionName = "Новый чат";

    /// <summary>
    /// Whether images are attached to the current message
    /// </summary>
    [ObservableProperty]
    private bool _hasImages;

    /// <summary>
    /// Preview information for attached images
    /// </summary>
    public List<ImagePreviewInfo> ImagePreviews { get; } = new();

    /// <summary>
    /// Base64-encoded images for the current message
    /// </summary>
    private List<string> _attachedImages { get; } = new();

    /// <summary>
    /// Pending tool confirmations
    /// </summary>
    public ObservableCollection<ToolConfirmation> PendingConfirmations { get; } = new();

    /// <summary>
    /// Whether there are pending tool confirmations
    /// </summary>
    [ObservableProperty]
    private bool _hasPendingConfirmations;

    private string SessionsDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions");

    /// <summary>
    /// Exposes config for UI access (e.g., theme toggle)
    /// </summary>
    public Config Config => _config;

    public ChatViewModel(Config config)
    {
        _config = config;
        _rootDir = Directory.GetCurrentDirectory();
        
        _ollamaClient = new OllamaClient(config.BaseUrl);
        _toolExecutor = new ToolExecutor();
        
        // Initialize UI properties from config
        BaseUrl = config.BaseUrl;
        SystemPrompt = config.SystemPrompt;
        WeatherApiKey = config.WeatherApiKey;
        ThinkingEnabled = config.ThinkingEnabled;
        SelectedThinkingDepth = config.ThinkingDepth;
        
        RegisterTools();
        
        // Set initial model
        if (!string.IsNullOrEmpty(config.Model))
        {
            SelectedModelDisplay = config.Model;
        }
        
        // Load all sessions
        LoadAllSessions();
        
        // Create initial session
        ActiveSession = new ChatSession { Name = "Новый чат", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ChatSessions.Add(ActiveSession!);
    }

    // Update config when properties change
    partial void OnBaseUrlChanged(string value)
    {
        _config.BaseUrl = value;
        _ollamaClient = new OllamaClient(value);
    }

    partial void OnSelectedModelDisplayChanged(string value)
    {
        // Find matching model and update config
        var model = AvailableModels.FirstOrDefault(m => m.DisplayName == value);
        if (model != null)
        {
            _config.Model = model.Name;
            Console.WriteLine($"🔄 Model changed to: {model.Name}");
        }
    }

    partial void OnSystemPromptChanged(string value)
    {
        _config.SystemPrompt = value;
    }

    partial void OnWeatherApiKeyChanged(string value)
    {
        _config.WeatherApiKey = value;
    }

    partial void OnThinkingEnabledChanged(bool value)
    {
        _config.ThinkingEnabled = value;
    }

    partial void OnSelectedThinkingDepthChanged(string value)
    {
        _config.ThinkingDepth = value;
    }

    private void RegisterTools()
    {
        // Filesystem tools
        FilesystemTools.Register(_toolExecutor, _rootDir);

        // Process tools
        ProcessTools.Register(_toolExecutor);

        // Git tools
        GitTools.Register(_toolExecutor);

        // Weather tools (only if API key is configured)
        if (!string.IsNullOrEmpty(_config.WeatherApiKey))
        {
            _weatherClient = new WeatherClient(_config.WeatherApiKey);
            WeatherTools.Register(_toolExecutor, _weatherClient);
        }

        // Web tools (always available)
        _webClient = new WebClient();
        WebTools.Register(_toolExecutor, _webClient);

        // Log registered tools
        var tools = _toolExecutor.GetTools();
        var toolNames = tools.Select(t => t.Function.Name).ToList();
        Console.WriteLine($"🔧 Registered {toolNames.Count} tools: [{string.Join(", ", toolNames)}]");
    }

    /// <summary>
    /// Load available models from Ollama
    /// </summary>
    public async Task LoadModelsAsync()
    {
        try
        {
            var models = await _ollamaClient.ListModelsAsync();
            
            if (models.Count == 0)
            {
                StatusText = "⚠️ Модели не найдены. Установите: ollama pull llama3.2";
                return;
            }

            AvailableModels = models;
            ModelOptions = new ObservableCollection<string>(models.Select(m => m.DisplayName));

            // Select current model if available
            if (!string.IsNullOrEmpty(_config.Model) && models.Any(m => m.Name == _config.Model))
            {
                SelectedModelDisplay = models.First(m => m.Name == _config.Model).DisplayName;
            }
            else if (models.Count > 0)
            {
                SelectedModelDisplay = models[0].DisplayName;
                _config.Model = models[0].Name;
            }

            var capSet = new HashSet<string>(models.SelectMany(m => m.Capabilities));
            StatusText = $"📦 {models.Count} моделей | Доступно: {string.Join(", ", capSet)}";
        }
        catch (Exception ex)
        {
            StatusText = $"⚠️ Ошибка загрузки моделей: {ex.Message}";
        }
    }

    /// <summary>
    /// Handle user message submission
    /// </summary>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) && _attachedImages.Count == 0) return;
        if (IsProcessing) return;

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        // Update session name from first message
        if (ActiveSession != null && ActiveSession.Messages.Count == 0)
        {
            ActiveSession.Name = (userMessage ?? "Изображение").Length > 50 
                ? (userMessage ?? "Изображение")[..50] + "..." 
                : (userMessage ?? "Изображение");
            SessionName = ActiveSession.Name;
        }

        // Add user message to UI (with image indicator if applicable)
        var displayText = string.IsNullOrEmpty(userMessage) 
            ? $"📷 Изображение ({_attachedImages.Count})" 
            : userMessage;
        var messageViewModel = new ChatMessageViewModel(displayText, ChatMessageViewModel.MessageType.User)
        {
            Images = _attachedImages.Count > 0 ? new List<string>(_attachedImages) : null
        };
        ChatMessages.Add(messageViewModel);
        
        // Add to conversation history
        var message = new Message { Role = "user", Content = userMessage };
        if (_attachedImages.Count > 0)
        {
            message.Images = new List<string>(_attachedImages);
        }
        ConversationHistory.Add(message);

        // Clear attached images
        _attachedImages.Clear();
        ImagePreviews.Clear();
        HasImages = false;

        // Process AI response with tool calling
        await ProcessAIResponseAsync(userMessage ?? string.Empty, _sendMessageCancellationToken.Token);
    }

    private CancellationTokenSource _sendMessageCancellationToken = new();

    /// <summary>
    /// Process AI response with multi-turn tool calling loop
    /// </summary>
    private async Task ProcessAIResponseAsync(string userText, CancellationToken cancellationToken = default)
    {
        IsProcessing = true;
        ShowTypingIndicator = true;
        StatusText = "Думаю...";

        try
        {
            // Build messages with system prompt
            var messages = new List<Message>();
            
            // System prompt
            if (!string.IsNullOrEmpty(_config.SystemPrompt))
            {
                messages.Add(new Message { Role = "system", Content = _config.SystemPrompt });
            }

            // Conversation history with sliding window
            var history = ConversationHistory.Count > _config.ContextLimit
                ? ConversationHistory.Skip(ConversationHistory.Count - _config.ContextLimit)
                : ConversationHistory;
            messages.AddRange(history);

            var hasTools = _toolExecutor.GetTools().Count > 0;
            var useTools = hasTools && ModelSupportsTools(_config.Model);
            var maxTurns = 10;

            for (var turn = 0; turn < maxTurns; turn++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = messages,
                    Stream = true
                };

                // Add thinking mode if enabled AND supported by model
                if (_config.ThinkingEnabled && ModelSupportsThinking(_config.Model))
                {
                    request.Think = _config.ThinkingDepth;
                    Console.WriteLine($"🧠 Thinking mode enabled with depth: {_config.ThinkingDepth}");
                }
                else if (_config.ThinkingEnabled && !ModelSupportsThinking(_config.Model))
                {
                    Console.WriteLine($"⚠️ Thinking not supported by model: {_config.Model}");
                    StatusText = "⚠️ Thinking не поддерживается этой моделью";
                }

                if (useTools)
                {
                    request.Tools = _toolExecutor.GetTools().Select(t => (object)t).ToList();
                    var toolNames = _toolExecutor.GetTools().Select(t => t.Function.Name).ToList();
                    Console.WriteLine($"🔧 Sending {toolNames.Count} tools: [{string.Join(", ", toolNames)}]");
                }
                else
                {
                    Console.WriteLine($"📝 No tools (hasTools={hasTools}, supported={ModelSupportsTools(_config.Model)}, model={_config.Model})");
                }

                // Stream the response
                var (fullText, toolCalls) = await StreamResponseAsync(request, cancellationToken);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                Console.WriteLine($"📝 Response text: {fullText?.Length} chars, Tool calls: {toolCalls.Count}");

                // Display AI response
                if (!string.IsNullOrEmpty(fullText))
                {
                    ChatMessages.Add(new ChatMessageViewModel(fullText, ChatMessageViewModel.MessageType.Assistant));
                }

                // If there are tool calls, execute them and continue
                if (toolCalls.Count > 0)
                {
                    StatusText = "Проверяю инструменты...";

                    // Add assistant message with tool calls to history
                    messages.Add(new Message
                    {
                        Role = "assistant",
                        Content = fullText ?? string.Empty,
                        ToolCalls = toolCalls
                    });

                    // Collect all tool results into a single message
                    var toolResults = new List<string>();
                    
                    // Execute each tool call
                    foreach (var toolCall in toolCalls)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Translate tool name to Russian
                        var toolNameRu = TranslateToolName(toolCall.Function.Name);
                        
                        // Show confirmation request for sensitive operations
                        var needsConfirmation = IsSensitiveTool(toolCall.Function.Name);
                        if (needsConfirmation)
                        {
                            var confirmation = new ToolConfirmation
                            {
                                ToolName = toolCall.Function.Name,
                                ToolNameRu = toolNameRu,
                                Arguments = System.Text.Json.JsonSerializer.Serialize(toolCall.Function.Arguments)
                            };
                            AddToolConfirmation(confirmation);
                            
                            // Wait for user confirmation asynchronously
                            try
                            {
                                await confirmation.CompletionSource.Task.WaitAsync(cancellationToken);
                            }
                            catch
                            {
                                // Cancellation or timeout - treat as rejected
                                confirmation.IsRejected = true;
                            }
                            
                            PendingConfirmations.Remove(confirmation);
                            HasPendingConfirmations = PendingConfirmations.Count > 0;
                            
                            if (confirmation.IsRejected)
                            {
                                toolResults.Add($"*❌ Инструмент отклонён пользователем: `{toolNameRu}`*");
                                messages.Add(new Message
                                {
                                    Role = "tool",
                                    Content = "Отклонено пользователем",
                                    ToolName = toolCall.Function.Name
                                });
                                continue;
                            }
                        }
                        
                        var result = await _toolExecutor.ExecuteAsync(toolCall);
                        
                        // Add to results collection
                        var status = result.IsError ? "❌" : "✅";
                        toolResults.Add($"*{status} Инструмент: `{toolNameRu}`*\n\n`{result.Content}`");

                        // Add tool result to messages
                        messages.Add(new Message
                        {
                            Role = "tool",
                            Content = result.Content,
                            ToolName = toolCall.Function.Name
                        });
                    }

                    // Display all tool results as a single message
                    var combinedToolMessage = string.Join("\n\n---\n\n", toolResults);
                    ChatMessages.Add(new ChatMessageViewModel(combinedToolMessage, ChatMessageViewModel.MessageType.ToolResult));

                    StatusText = "Думаю...";
                    continue; // Continue the loop
                }

                // No tool calls - final response
                if (!string.IsNullOrEmpty(fullText))
                {
                    ConversationHistory.Add(new Message
                    {
                        Role = "assistant",
                        Content = fullText
                    });
                }

                break;
            }

            StatusText = "Готово";
            
            // Save session after successful response
            SaveCurrentSession();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Отменено";
            ChatMessages.Add(new ChatMessageViewModel("⏹ Запрос отменён", ChatMessageViewModel.MessageType.Error));
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
            ChatMessages.Add(new ChatMessageViewModel($"❌ Ошибка: {ex.Message}", ChatMessageViewModel.MessageType.Error));
        }
        finally
        {
            ShowTypingIndicator = false;
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Translate tool name to Russian
    /// </summary>
    private static string TranslateToolName(string name)
    {
        return name.ToLower() switch
        {
            "read_file" => "Чтение файла",
            "write_file" => "Запись файла",
            "list_directory" => "Список директории",
            "create_directory" => "Создание директории",
            "search_files" => "Поиск файлов",
            "get_process_list" => "Список процессов",
            "kill_process" => "Завершение процесса",
            "run_command" => "Выполнение команды",
            "git_status" => "Статус Git",
            "git_diff" => "Изменения Git",
            "git_log" => "История Git",
            "git_branch" => "Ветки Git",
            "git_add" => "Добавление в Git",
            "git_commit" => "Коммит Git",
            "get_weather" => "Погода",
            "web_search" => "Поиск в интернете",
            "web_fetch" => "Загрузка страницы",
            _ => name
        };
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
        var toolSupportingFamilies = new[] { "llama", "gemma", "mistral", "mixtral" };
        var toolSupportingNames = new[] { "llama3.1", "llama3.2", "gemma2", "gemma3", "mistral-large" };
        
        foreach (var family in toolSupportingFamilies)
        {
            if (nameLower.Contains(family)) return true;
        }
        
        foreach (var name in toolSupportingNames)
        {
            if (nameLower.Contains(name)) return true;
        }
        
        // Models known NOT to support tools
        var noToolNames = new[] { "saiga", "falcon", "stable-code", "tinyllama", "deepseek", "codellama", "codegemma", "qwen" };
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

    /// <summary>
    /// Check if a tool requires user confirmation before execution
    /// </summary>
    private static bool IsSensitiveTool(string toolName)
    {
        return toolName.ToLower() switch
        {
            "run_command" => true,
            "write_file" => true,
            "delete_file" => true,
            "create_directory" => true,
            "kill_process" => true,
            _ => false
        };
    }

    /// <summary>
    /// Stream response from Ollama and extract text and tool calls
    /// </summary>
    private async Task<(string? text, List<ToolCall> toolCalls)> StreamResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
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
                    Console.WriteLine($"📨 Chunk: Content={chunk.Message.Content?.Length ?? 0} chars, ToolCalls={chunk.Message.ToolCalls?.Count ?? 0}, Done={chunk.Done}");
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
                Console.WriteLine($"Error parsing chunk: {ex.Message}");
            }
        }

        return (string.IsNullOrEmpty(fullText) ? null : fullText, toolCalls);
    }

    /// <summary>
    /// Toggle settings visibility
    /// </summary>
    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    /// <summary>
    /// Save configuration
    /// </summary>
    [RelayCommand]
    private void SaveConfig(string path)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            StatusText = $"✅ Конфиг сохранён в {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка сохранения: {ex.Message}";
        }
    }

    /// <summary>
    /// Load configuration
    /// </summary>
    [RelayCommand]
    private void LoadConfig(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<Config>(json);
            if (loaded != null)
            {
                _config = loaded;
                
                // Update UI properties
                BaseUrl = loaded.BaseUrl;
                SystemPrompt = loaded.SystemPrompt;
                WeatherApiKey = loaded.WeatherApiKey;
                ThinkingEnabled = loaded.ThinkingEnabled;
                SelectedThinkingDepth = loaded.ThinkingDepth;
                
                StatusText = $"✅ Конфиг загружен из {path}";
                
                // Re-register tools
                _toolExecutor = new ToolExecutor();
                RegisterTools();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка загрузки: {ex.Message}";
        }
    }

    /// <summary>
    /// Clear chat history
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
        ConversationHistory.Clear();
        StatusText = "Чат очищен";
    }

    /// <summary>
    /// Set status text (for programmatic use)
    /// </summary>
    public void SetStatus(string text)
    {
        StatusText = text;
    }

    /// <summary>
    /// Create a new chat session
    /// </summary>
    [RelayCommand]
    private void NewChat()
    {
        SaveCurrentSession();
        
        ChatMessages.Clear();
        ConversationHistory.Clear();
        SessionName = "Новый чат";
        ActiveSession = new ChatSession { Name = "Новый чат", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ChatSessions.Add(ActiveSession!);
        StatusText = "Создан новый чат";
    }

    /// <summary>
    /// Save current session to disk
    /// </summary>
    private void SaveCurrentSession()
    {
        if (ActiveSession == null) return;
        
        ActiveSession.UpdatedAt = DateTime.UtcNow;
        ActiveSession.Messages.Clear();
        
        foreach (var msg in ConversationHistory)
        {
            ActiveSession.Messages.Add(new ChatMessage
            {
                Role = msg.Role,
                Content = msg.Content,
                ToolName = msg.ToolName,
                ToolCalls = msg.ToolCalls?.Select(tc => new ChatToolCall
                {
                    Function = new ChatFunctionCall
                    {
                        Name = tc.Function.Name,
                        Arguments = tc.Function.Arguments
                    }
                }).ToList()
            });
        }

        ActiveSession.Model = _config.Model;

        try
        {
            var dir = SessionsDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{ActiveSession.Id}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(ActiveSession, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Load a chat session by ID
    /// </summary>
    public void LoadSession(string sessionId)
    {
        SaveCurrentSession();
        
        try
        {
            var dir = SessionsDir;
            var path = Path.Combine(dir, $"{sessionId}.json");
            
            if (!File.Exists(path)) return;
            
            var json = File.ReadAllText(path);
            var session = System.Text.Json.JsonSerializer.Deserialize<ChatSession>(json);
            if (session == null) return;
            
            ActiveSession = session;
            SessionName = session.Name;
            
            ConversationHistory.Clear();
            ChatMessages.Clear();
            
            foreach (var msg in session.Messages)
            {
                ConversationHistory.Add(new Message
                {
                    Role = msg.Role,
                    Content = msg.Content,
                    ToolName = msg.ToolName,
                    ToolCalls = msg.ToolCalls?.Select(tc => new ToolCall
                    {
                        Function = new FunctionCall
                        {
                            Name = tc.Function.Name,
                            Arguments = tc.Function.Arguments
                        }
                    }).ToList()
                });
                
                // Add to UI
                var type = msg.Role switch
                {
                    "user" => ChatMessageViewModel.MessageType.User,
                    "assistant" => ChatMessageViewModel.MessageType.Assistant,
                    "tool" => ChatMessageViewModel.MessageType.ToolResult,
                    _ => ChatMessageViewModel.MessageType.Assistant
                };
                
                ChatMessages.Add(new ChatMessageViewModel(msg.Content, type));
            }
            
            if (!string.IsNullOrEmpty(session.Model))
            {
                _config.Model = session.Model;
            }
            
            StatusText = $"Чат загружен: {session.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки: {ex.Message}";
        }
    }

    /// <summary>
    /// Delete a chat session
    /// </summary>
    [RelayCommand]
    private void DeleteChat(string sessionId)
    {
        try
        {
            var dir = SessionsDir;
            var path = Path.Combine(dir, $"{sessionId}.json");
            if (File.Exists(path)) File.Delete(path);
            
            var session = ChatSessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                ChatSessions.Remove(session);
                
                if (ActiveSession?.Id == sessionId)
                {
                    NewChatCommand.Execute(null);
                }
            }
            
            StatusText = "Чат удалён";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка удаления: {ex.Message}";
        }
    }

    /// <summary>
    /// Load all sessions on startup
    /// </summary>
    private void LoadAllSessions()
    {
        try
        {
            var dir = SessionsDir;
            Directory.CreateDirectory(dir);
            
            var files = Directory.GetFiles(dir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var session = System.Text.Json.JsonSerializer.Deserialize<ChatSession>(json);
                    if (session != null)
                    {
                        ChatSessions.Add(session);
                    }
                }
                catch
                {
                    // Skip invalid files
                }
            }
        }
        catch
        {
            // Ignore errors on startup
        }
    }

    /// <summary>
    /// Attach images to the current message
    /// </summary>
    public async Task AttachImagesAsync(List<string> filePaths)
    {
        try
        {
            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;

                var bytes = await File.ReadAllBytesAsync(filePath);
                var base64 = Convert.ToBase64String(bytes);
                _attachedImages.Add(base64);

                var fileName = Path.GetFileName(filePath);
                var size = bytes.Length;
                ImagePreviews.Add(new ImagePreviewInfo { Name = fileName, Size = size });
            }

            HasImages = _attachedImages.Count > 0;
            StatusText = $"📷 Прикреплено изображений: {_attachedImages.Count}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка загрузки изображений: {ex.Message}";
        }
    }

    /// <summary>
    /// Remove an attached image
    /// </summary>
    public void RemoveImage(ImagePreviewInfo preview)
    {
        var index = ImagePreviews.IndexOf(preview);
        if (index >= 0)
        {
            _attachedImages.RemoveAt(index);
            ImagePreviews.Remove(preview);
            HasImages = _attachedImages.Count > 0;
            StatusText = $"📷 Прикреплено изображений: {_attachedImages.Count}";
        }
    }

    /// <summary>
    /// Remove a tool confirmation from pending list
    /// </summary>
    public void RemoveFromPendingConfirmations(ToolConfirmation confirmation)
    {
        PendingConfirmations.Remove(confirmation);
        HasPendingConfirmations = PendingConfirmations.Count > 0;
    }

    /// <summary>
    /// Add a tool confirmation request
    /// </summary>
    public void AddToolConfirmation(ToolConfirmation confirmation)
    {
        PendingConfirmations.Add(confirmation);
        HasPendingConfirmations = true;
    }

    /// <summary>
    /// Toggle theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _config.Theme = _config.Theme switch
        {
            "system" => "dark",
            "dark" => "light",
            _ => "system"
        };

        // Apply theme to the main window
        ApplyThemeToWindow();
    }

    private void ApplyThemeToWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var variant = _config.Theme switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => null // system theme
            };

            if (variant != null && desktop.MainWindow is Window window)
            {
                window.RequestedThemeVariant = variant;
            }
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        // Save current session before disposing
        SaveCurrentSession();
        
        _ollamaClient?.Dispose();
        _webClient?.Dispose();
        _weatherClient?.Dispose();
    }
}

/// <summary>
/// Preview information for attached images
/// </summary>
public class ImagePreviewInfo
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
}

/// <summary>
/// Tool confirmation request
/// </summary>
public class ToolConfirmation
{
    public string ToolName { get; set; } = string.Empty;
    public string ToolNameRu { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public bool IsRejected { get; set; }
    public bool IsPending => !IsApproved && !IsRejected;
    
    internal TaskCompletionSource<bool> CompletionSource { get; } = new();
}
