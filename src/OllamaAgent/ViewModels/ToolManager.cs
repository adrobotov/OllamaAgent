using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Services;
using OllamaAgent.Tools;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Manages tool registration, execution, and user confirmations.
/// </summary>
public partial class ToolManager
{
    private readonly ToolExecutor _toolExecutor;
    private readonly string _rootDir;
    private readonly ILogger _logger;
    private WebClient? _webClient;

    public ToolManager(ToolExecutor toolExecutor, string rootDir, ILogger logger)
    {
        _toolExecutor = toolExecutor;
        _rootDir = rootDir;
        _logger = logger;
    }

    /// <summary>
    /// Pending tool confirmations
    /// </summary>
    public ObservableCollection<ToolConfirmation> PendingConfirmations { get; } = new();

    /// <summary>
    /// Whether there are pending tool confirmations
    /// </summary>
    public bool HasPendingConfirmations => PendingConfirmations.Count > 0;

    /// <summary>
    /// Register only synchronous tools (called from constructor)
    /// </summary>
    public void RegisterSyncTools()
    {
        // Filesystem tools
        FilesystemTools.Register(_toolExecutor, _rootDir);

        // Process tools
        ProcessTools.Register(_toolExecutor);

        // Git tools
        GitTools.Register(_toolExecutor);

        // Web tools (always available)
        _webClient = new WebClient();
        WebTools.Register(_toolExecutor, _webClient);
    }

    /// <summary>
    /// Initialize async components (MCP tools, etc.)
    /// </summary>
    public async Task InitializeAsync(Config config)
    {
        // Weather tool via MCP (only if API key is configured)
        if (!string.IsNullOrEmpty(config.WeatherApiKey))
        {
            try
            {
                await McpWeatherTool.InitializeAsync(config.WeatherApiKey);
                McpWeatherTool.Register(_toolExecutor);
                _logger.Log("🌤️ Weather tool registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Weather tool init error: {ex.Message}");
            }
        }

        // Memory MCP (always available)
        try
        {
            var memInit = await McpMemoryTool.InitializeAsync();
            if (memInit)
            {
                McpMemoryTool.Register(_toolExecutor);
                _logger.Log("🧠 Memory MCP registered successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Memory MCP init error: {ex.Message}");
        }

        // Database MCP (if any connection string is configured)
        var dbConnections = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(config.DbPostgres))
            dbConnections["DATABASE_URL"] = config.DbPostgres;
        if (!string.IsNullOrEmpty(config.DbMySql))
            dbConnections["MYSQL_URL"] = config.DbMySql;
        if (!string.IsNullOrEmpty(config.DbSqlite))
            dbConnections["SQLITE_PATH"] = config.DbSqlite;

        if (dbConnections.Count > 0)
        {
            try
            {
                var dbInit = await McpDatabaseTool.InitializeAsync(dbConnections);
                if (dbInit)
                {
                    McpDatabaseTool.Register(_toolExecutor);
                    _logger.Log($"🗄️ Database MCP registered successfully ({dbConnections.Count} DB(s))");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Database MCP init error: {ex.Message}");
            }
        }

        // Log all registered tools
        var allTools = _toolExecutor.GetTools();
        _logger.Log($"📦 Total tools registered: {allTools.Count}");
        foreach (var tool in allTools)
        {
            _logger.Log($"  - {tool.Function.Name}: {tool.Function.Description}");
        }
    }

    /// <summary>
    /// Execute tool calls and collect results
    /// </summary>
    public async Task ExecuteToolCallsAsync(
        List<ToolCall> toolCalls,
        List<Message> messages,
        ChatMessageManager messageManager,
        string currentModel,
        Action<string> setStatus,
        CancellationToken cancellationToken)
    {
        messages.Add(new Message
        {
            Role = "assistant",
            Content = string.Empty,
            ToolCalls = toolCalls
        });

        var toolResults = new List<string>();

        foreach (var toolCall in toolCalls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toolNameRu = TranslateToolName(toolCall.Function.Name);
            var needsConfirmation = IsSensitiveTool(toolCall.Function.Name);

            if (needsConfirmation)
            {
                var confirmed = await RequestToolConfirmationAsync(toolCall, toolNameRu, cancellationToken);
                if (!confirmed) continue;
            }

            var result = await _toolExecutor.ExecuteAsync(toolCall);

            var status = result.IsError ? "❌" : "✅";
            toolResults.Add($"*{status} Инструмент: `{toolNameRu}`*\n\n`{result.Content}`");

            messages.Add(new Message
            {
                Role = "tool",
                Content = result.Content,
                ToolName = toolCall.Function.Name
            });
        }

        var combinedToolMessage = string.Join("\n\n---\n\n", toolResults);
        messageManager.AddToolResultMessage(combinedToolMessage);
    }

    /// <summary>
    /// Request user confirmation for a sensitive tool execution
    /// </summary>
    public async Task<bool> RequestToolConfirmationAsync(
        ToolCall toolCall,
        string toolNameRu,
        CancellationToken cancellationToken)
    {
        var confirmation = new ToolConfirmation
        {
            ToolName = toolCall.Function.Name,
            ToolNameRu = toolNameRu,
            Arguments = JsonSerializer.Serialize(toolCall.Function.Arguments)
        };
        AddToolConfirmation(confirmation);

        try
        {
            await confirmation.CompletionSource.Task.WaitAsync(cancellationToken);
        }
        catch
        {
            confirmation.IsRejected = true;
        }

        PendingConfirmations.Remove(confirmation);

        if (confirmation.IsRejected)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Add a tool confirmation request
    /// </summary>
    public void AddToolConfirmation(ToolConfirmation confirmation)
    {
        PendingConfirmations.Add(confirmation);
    }

    /// <summary>
    /// Remove a tool confirmation from pending list
    /// </summary>
    public void RemoveFromPendingConfirmations(ToolConfirmation confirmation)
    {
        PendingConfirmations.Remove(confirmation);
    }

    /// <summary>
    /// Translate tool name to Russian
    /// </summary>
    public static string TranslateToolName(string name)
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
    /// Dispose WebClient if created
    /// </summary>
    public void Dispose()
    {
        _webClient?.Dispose();
    }
}
