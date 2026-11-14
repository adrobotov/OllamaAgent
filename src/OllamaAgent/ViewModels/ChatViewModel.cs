using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Готово";

    [ObservableProperty]
    private bool _isProcessing;

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

    private ObservableCollection<string> _modelOptions = new();

    public ObservableCollection<string> ModelOptions
    {
        get => _modelOptions;
        set => SetProperty(ref _modelOptions, value);
    }

    public ObservableCollection<ChatMessageViewModel> ChatMessages { get; set; } = new();

    public List<Message> ConversationHistory { get; set; } = new();

    public List<ModelInfo> AvailableModels { get; set; } = new();

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
        
        RegisterTools();
        
        // Set initial model
        if (!string.IsNullOrEmpty(config.Model))
        {
            SelectedModelDisplay = config.Model;
        }
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
            var weatherClient = new WeatherClient(_config.WeatherApiKey);
            WeatherTools.Register(_toolExecutor, weatherClient);
        }

        // Web tools (always available)
        var webClient = new WebClient();
        WebTools.Register(_toolExecutor, webClient);

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
        if (string.IsNullOrWhiteSpace(InputText) || IsProcessing) return;

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        // Add user message to UI
        ChatMessages.Add(new ChatMessageViewModel(userMessage, ChatMessageViewModel.MessageType.User));
        
        // Add to conversation history
        ConversationHistory.Add(new Message { Role = "user", Content = userMessage });

        // Process AI response with tool calling
        await ProcessAIResponseAsync(userMessage);
    }

    /// <summary>
    /// Process AI response with multi-turn tool calling loop
    /// </summary>
    private async Task ProcessAIResponseAsync(string userText)
    {
        IsProcessing = true;
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
                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = messages,
                    Stream = true
                };

                if (useTools)
                {
                    request.Tools = _toolExecutor.GetTools().Select(t => (object)t).ToList();
                    var toolNames = _toolExecutor.GetTools().Select(t => t.Function.Name).ToList();
                    Console.WriteLine($"🔧 Sending {toolNames.Count} tools: [{string.Join(", ", toolNames)}]");
                }
                else
                {
                    Console.WriteLine($"📝 No tools (hasTools={hasTools}, supported={ModelSupportsTools(_config.Model)})");
                }

                // Stream the response
                var (fullText, toolCalls) = await StreamResponseAsync(request);
                
                Console.WriteLine($"📝 Response text: {fullText?.Length} chars, Tool calls: {toolCalls.Count}");

                // Display AI response
                if (!string.IsNullOrEmpty(fullText))
                {
                    ChatMessages.Add(new ChatMessageViewModel(fullText, ChatMessageViewModel.MessageType.Assistant));
                }

                // If there are tool calls, execute them and continue
                if (toolCalls.Count > 0)
                {
                    StatusText = "Выполняю инструменты...";

                    // Add assistant message with tool calls to history
                    messages.Add(new Message
                    {
                        Role = "assistant",
                        Content = fullText ?? string.Empty,
                        ToolCalls = toolCalls
                    });

                    // Execute each tool call
                    foreach (var toolCall in toolCalls)
                    {
                        var result = await _toolExecutor.ExecuteAsync(toolCall);
                        
                        // Translate tool name to Russian
                        var toolNameRu = TranslateToolName(toolCall.Function.Name);
                        
                        // Display tool call result
                        var status = result.IsError ? "❌" : "✅";
                        var toolMessage = $"\n\n*{status} Инструмент: `{toolNameRu}`*\n\n`{result.Content}`";
                        ChatMessages.Add(new ChatMessageViewModel(toolMessage, ChatMessageViewModel.MessageType.ToolResult));

                        // Add tool result to messages
                        messages.Add(new Message
                        {
                            Role = "tool",
                            Content = result.Content,
                            ToolName = toolCall.Function.Name
                        });
                    }

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
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
            ChatMessages.Add(new ChatMessageViewModel($"❌ Ошибка: {ex.Message}", ChatMessageViewModel.MessageType.Error));
        }
        finally
        {
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
        
        // Models known to support tools
        var toolSupportingFamilies = new[] { "llama", "gemma", "qwen", "mistral", "mixtral", "codellama", "codegemma" };
        var toolSupportingNames = new[] { "llama3.1", "llama3.2", "gemma2", "gemma3", "qwen2.5", "qwen3", "mistral-large", "mixtral" };
        
        foreach (var family in toolSupportingFamilies)
        {
            if (nameLower.Contains(family)) return true;
        }
        
        foreach (var name in toolSupportingNames)
        {
            if (nameLower.Contains(name)) return true;
        }
        
        // Models known NOT to support tools
        var noToolNames = new[] { "saiga", "falcon", "stable-code", "tinyllama" };
        foreach (var name in noToolNames)
        {
            if (nameLower.Contains(name)) return false;
        }
        
        // Default: assume no tool support for unknown models
        return false;
    }

    /// <summary>
    /// Stream response from Ollama and extract text and tool calls
    /// </summary>
    private async Task<(string? text, List<ToolCall> toolCalls)> StreamResponseAsync(ChatRequest request)
    {
        await using var response = await _ollamaClient.ChatStreamAsync(request);
        
        var fullText = string.Empty;
        var toolCalls = new List<ToolCall>();
        var doneThinking = false;

        using var reader = new StreamReader(response);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
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
    }
}
