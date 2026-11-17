using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OllamaAgent.Helpers;
using OllamaAgent.Models;
using OllamaAgent.Services;
using OllamaAgent.Tools;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Main chat view model — composes smaller managers following SRP.
/// </summary>
public partial class ChatViewModel : ObservableObject, IDisposable
{
    private OllamaClient _ollamaClient;
    private ToolExecutor _toolExecutor;
    private Config _config;
    private readonly string _rootDir;
    private readonly ILogger _logger;

    // Composed managers
    private readonly ChatSessionManager _sessionManager;
    private readonly ChatMessageManager _messageManager;
    private readonly ToolManager _toolManager;
    private readonly ModelManager _modelManager;
    private readonly ImageManager _imageManager;
    private readonly AIResponseProcessor _responseProcessor;

    // Config auto-save timer
    private System.Timers.Timer? _autoSaveTimer;

    // Constructor
    public ChatViewModel(Config config) : this(config, new ConsoleLogger()) { }

    internal ChatViewModel(Config config, ILogger logger)
    {
        _config = config;
        _rootDir = Directory.GetCurrentDirectory();
        _logger = logger;

        // Set global logger for static tool classes
        AppLogger.Logger = logger;

        _ollamaClient = new OllamaClient(config.BaseUrl);
        _toolExecutor = new ToolExecutor();

        // Initialize managers
        _messageManager = new ChatMessageManager();
        _imageManager = new ImageManager();
        _sessionManager = new ChatSessionManager(ChatSessions, logger);
        _toolManager = new ToolManager(_toolExecutor, _rootDir, logger);
        _modelManager = new ModelManager(_ollamaClient, config, logger);
        _responseProcessor = new AIResponseProcessor(
            _ollamaClient, _toolExecutor, config, logger,
            _messageManager, _toolManager);

        // Initialize UI properties from config
        BaseUrl = config.BaseUrl;
        SystemPrompt = config.SystemPrompt;
        WeatherApiKey = config.WeatherApiKey;
        ThinkingEnabled = config.ThinkingEnabled;
        SelectedThinkingDepth = config.ThinkingDepth;

        // Register sync tools
        _toolManager.RegisterSyncTools();

        // Set initial model
        if (!string.IsNullOrEmpty(config.Model))
        {
            _modelManager.SelectedModelDisplay = config.Model;
        }

        // Load all sessions
        _sessionManager.LoadAllSessions();

        // Create initial session
        _sessionManager.ActiveSession = new ChatSession { Name = "Новый чат", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ChatSessions.Add(_sessionManager.ActiveSession!);

        // Setup auto-save
        _autoSaveTimer = new System.Timers.Timer(1000);
        _autoSaveTimer.AutoReset = false;
        _autoSaveTimer.Elapsed += (_, _) => SaveConfigAuto();
    }

    // === Config Properties ===

    private string _inputText = string.Empty;
    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    private string _statusText = "Готово";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private bool _showTypingIndicator;
    public bool ShowTypingIndicator
    {
        get => _showTypingIndicator;
        set => SetProperty(ref _showTypingIndicator, value);
    }

    private bool _showSettings;
    public bool ShowSettings
    {
        get => _showSettings;
        set => SetProperty(ref _showSettings, value);
    }

    private string _selectedModelDisplay = string.Empty;
    public string SelectedModelDisplay
    {
        get => _selectedModelDisplay;
        set => SetProperty(ref _selectedModelDisplay, value);
    }

    private string _baseUrl;
    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (SetProperty(ref _baseUrl, value))
            {
                _config.BaseUrl = value;
                _ollamaClient = new OllamaClient(value);
                SaveConfigAuto();
            }
        }
    }

    private string _systemPrompt;
    public string SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (SetProperty(ref _systemPrompt, value))
            {
                _config.SystemPrompt = value;
                SaveConfigAuto();
            }
        }
    }

    private string _weatherApiKey;
    public string WeatherApiKey
    {
        get => _weatherApiKey;
        set
        {
            if (SetProperty(ref _weatherApiKey, value))
            {
                _config.WeatherApiKey = value;
                SaveConfigAuto();
            }
        }
    }

    private bool _thinkingEnabled;
    public bool ThinkingEnabled
    {
        get => _thinkingEnabled;
        set
        {
            if (SetProperty(ref _thinkingEnabled, value))
            {
                _config.ThinkingEnabled = value;
                SaveConfigAuto();
            }
        }
    }

    private string _selectedThinkingDepth;
    public string SelectedThinkingDepth
    {
        get => _selectedThinkingDepth;
        set
        {
            if (SetProperty(ref _selectedThinkingDepth, value))
            {
                _config.ThinkingDepth = value;
                SaveConfigAuto();
            }
        }
    }

    private string _sessionSearchText = string.Empty;
    public string SessionSearchText
    {
        get => _sessionSearchText;
        set => SetProperty(ref _sessionSearchText, value);
    }

    private string _chatSearchText = string.Empty;
    public string ChatSearchText
    {
        get => _chatSearchText;
        set => SetProperty(ref _chatSearchText, value);
    }

    /// <summary>
    /// Observable collection of chat sessions (UI binding)
    /// </summary>
    public ObservableCollection<ChatSession> ChatSessions { get; } = new();

    /// <summary>
    /// Observable collection of chat message view models (UI binding)
    /// </summary>
    public ObservableCollection<ChatMessageViewModel> ChatMessages => _messageManager.ChatMessages;

    /// <summary>
    /// Conversation history for API (UI binding)
    /// </summary>
    public List<Message> ConversationHistory => _messageManager.ConversationHistory;

    /// <summary>
    /// Filtered sessions based on search text (uses fuzzy matching)
    /// </summary>
    public IEnumerable<ChatSession> FilteredSessions =>
        string.IsNullOrWhiteSpace(SessionSearchText)
            ? ChatSessions
            : ChatSessions.Where(s =>
                FuzzySearch.Contains(s.Name, SessionSearchText) ||
                s.Messages.Any(m => FuzzySearch.Contains(m.Content, SessionSearchText)));

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
    /// Grouped sessions by date category
    /// </summary>
    public SessionGroup[] GroupedSessions => _sessionManager.GroupedSessions;

    /// <summary>
    /// Currently active session
    /// </summary>
    public ChatSession? ActiveSession => _sessionManager.ActiveSession;

    /// <summary>
    /// Current session name for display
    /// </summary>
    public string SessionName
    {
        get => _sessionManager.SessionName;
        set => _sessionManager.SessionName = value;
    }

    /// <summary>
    /// List of available models from Ollama
    /// </summary>
    public List<ModelInfo> AvailableModels => _modelManager.AvailableModels;

    /// <summary>
    /// Observable collection of model display names for UI binding
    /// </summary>
    public ObservableCollection<string> ModelOptions => _modelManager.ModelOptions;

    /// <summary>
    /// Preview information for attached images
    /// </summary>
    public List<ImagePreviewInfo> ImagePreviews => _imageManager.ImagePreviews;

    /// <summary>
    /// Pending tool confirmations
    /// </summary>
    public ObservableCollection<ToolConfirmation> PendingConfirmations => _toolManager.PendingConfirmations;

    /// <summary>
    /// Whether there are pending tool confirmations
    /// </summary>
    public bool HasPendingConfirmations => _toolManager.HasPendingConfirmations;

    /// <summary>
    /// Exposes config for UI access (e.g., theme toggle)
    /// </summary>
    public Config Config => _config;

    private string SessionsDir => ConfigPaths.SessionsDir;

    // === Commands ===

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsProcessing) return;

        var userText = InputText.Trim();
        InputText = string.Empty;
        var images = _imageManager.GetAndClearImages();

        await _responseProcessor.SendMessageAsync(
            userText,
            CancellationToken.None,
            SetStatus,
            v => IsProcessing = v,
            v => ShowTypingIndicator = v,
            v => SessionName = v,
            _ => { },
            images);
    }

    /// <summary>
    /// Load available models from Ollama
    /// </summary>
    public async Task LoadModelsAsync()
    {
        await _modelManager.LoadModelsAsync(SetStatus);
    }

    /// <summary>
    /// Toggle theme between light and dark
    /// </summary>
    public void ToggleTheme()
    {
        var current = Application.Current?.RequestedThemeVariant;
        var newTheme = current == ThemeVariant.Dark ? "light" : "dark";
        SetTheme(newTheme);
    }

    [RelayCommand]
    private void NewChat()
    {
        _sessionManager.CreateNew();
        _messageManager.ClearChat();
    }

    [RelayCommand]
    private void ClearChat()
    {
        _messageManager.ClearChat();
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    [RelayCommand]
    private void SaveConfig(string? path = null)
    {
        try
        {
            var filePath = string.IsNullOrEmpty(path) ? ConfigPaths.ConfigFile : path;
            var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            SetStatus("✅ Конфигурация сохранена");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Ошибка сохранения: {ex.Message}");
        }
    }

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
                BaseUrl = loaded.BaseUrl;
                SystemPrompt = loaded.SystemPrompt;
                WeatherApiKey = loaded.WeatherApiKey;
                ThinkingEnabled = loaded.ThinkingEnabled;
                SelectedThinkingDepth = loaded.ThinkingDepth;
                _ollamaClient = new OllamaClient(loaded.BaseUrl);
                SetStatus("✅ Конфигурация загружена");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Ошибка загрузки: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteChat(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        _sessionManager.DeleteSession(sessionId);
        if (_sessionManager.ActiveSession?.Id == sessionId)
        {
            NewChatCommand.Execute(null);
        }
    }

    /// <summary>
    /// Attach images to the current message
    /// </summary>
    public async Task AttachImagesAsync(List<string> filePaths)
    {
        await _imageManager.AttachImagesAsync(filePaths, SetStatus);
    }

    /// <summary>
    /// Remove an attached image
    /// </summary>
    public void RemoveImage(ImagePreviewInfo preview)
    {
        _imageManager.RemoveImage(preview, SetStatus);
    }

    // === Session Management ===

    /// <summary>
    /// Save current session to disk
    /// </summary>
    public void SaveCurrentSession()
    {
        _sessionManager.SaveCurrentSession(_config, ConversationHistory);
    }

    /// <summary>
    /// Load a chat session by ID
    /// </summary>
    public void LoadSession(string sessionId)
    {
        var (session, messages) = _sessionManager.LoadSession(sessionId);
        if (session == null) return;

        _sessionManager.ActiveSession = session;
        SessionName = session.Name;

        _messageManager.ConversationHistory.Clear();
        _messageManager.ChatMessages.Clear();

        foreach (var msg in messages)
        {
            _messageManager.ConversationHistory.Add(msg);
            var type = msg.Role switch
            {
                "user" => ChatMessageViewModel.MessageType.User,
                "assistant" => ChatMessageViewModel.MessageType.Assistant,
                "tool" => ChatMessageViewModel.MessageType.ToolResult,
                _ => ChatMessageViewModel.MessageType.User
            };
            _messageManager.ChatMessages.Add(new ChatMessageViewModel(msg.Content, type));
        }
    }

    /// <summary>
    /// Load all sessions from disk
    /// </summary>
    public void LoadAllSessions()
    {
        _sessionManager.LoadAllSessions();
    }

    // === Export / Import ===

    /// <summary>
    /// Export current session to JSON string
    /// </summary>
    public string ExportSessionToJson()
    {
        return _sessionManager.ExportSessionToJson();
    }

    /// <summary>
    /// Export current session to Markdown string
    /// </summary>
    public string ExportSessionToMarkdown()
    {
        return _sessionManager.ExportSessionToMarkdown();
    }

    /// <summary>
    /// Import sessions from JSON string
    /// </summary>
    public int ImportSessionsFromJson(string json)
    {
        return _sessionManager.ImportSessionsFromJson(json);
    }

    /// <summary>
    /// Initialize async components (MCP tools) — must be called after construction
    /// </summary>
    public async Task InitializeAsync()
    {
        await _toolManager.InitializeAsync(_config);
    }

    /// <summary>
    /// Remove a tool confirmation from pending list
    /// </summary>
    public void RemoveFromPendingConfirmations(ToolConfirmation confirmation)
    {
        _toolManager.RemoveFromPendingConfirmations(confirmation);
    }

    // === Theme ===

    public void SetTheme(string theme)
    {
        if (Application.Current == null) return;

        var variant = theme.ToLower() switch
        {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => null
        };

        if (variant != null)
        {
            Application.Current.RequestedThemeVariant = variant;
            _config.Theme = theme;
            SaveConfigAuto();
        }
    }

    // === Status ===

    public void SetStatus(string text)
    {
        StatusText = text;
    }

    // === Tool Confirmation ===

    public void ApproveTool(ToolConfirmation confirmation)
    {
        confirmation.IsRejected = false;
        confirmation.CompletionSource.SetResult(true);
    }

    public void RejectTool(ToolConfirmation confirmation)
    {
        confirmation.IsRejected = true;
        confirmation.CompletionSource.SetResult(false);
    }

    // === Auto-save ===

    private void SaveConfigAuto()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Start();
    }

    // === IDisposable ===

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _toolManager.Dispose();
    }
}
