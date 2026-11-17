using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using OllamaAgent.Helpers;
using OllamaAgent.Models;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Manages chat session CRUD, filtering, and export/import operations.
/// </summary>
public partial class ChatSessionManager
{
    private readonly ObservableCollection<ChatSession> _sessions;
    private readonly string _sessionsDir;
    private readonly ILogger _logger;

    public ChatSessionManager(ObservableCollection<ChatSession> sessions, ILogger logger)
    {
        _sessions = sessions;
        _sessionsDir = ConfigPaths.SessionsDir;
        _logger = logger;
    }

    /// <summary>
    /// Currently active session
    /// </summary>
    public ChatSession? ActiveSession { get; set; }

    /// <summary>
    /// Current session name for display
    /// </summary>
    public string SessionName
    {
        get => ActiveSession?.Name ?? "Новый чат";
        set
        {
            if (ActiveSession != null)
                ActiveSession.Name = value;
        }
    }

    /// <summary>
    /// List of all chat sessions
    /// </summary>
    public ObservableCollection<ChatSession> ChatSessions => _sessions;

    /// <summary>
    /// Search text for filtering sessions
    /// </summary>
    public string SessionSearchText { get; set; } = string.Empty;

    /// <summary>
    /// Filtered sessions based on search text (uses fuzzy matching)
    /// </summary>
    public IEnumerable<ChatSession> FilteredSessions =>
        string.IsNullOrWhiteSpace(SessionSearchText)
            ? _sessions
            : _sessions.Where(s =>
                FuzzySearch.Contains(s.Name, SessionSearchText) ||
                s.Messages.Any(m => FuzzySearch.Contains(m.Content, SessionSearchText)));

    /// <summary>
    /// Grouped sessions by date category
    /// </summary>
    public SessionGroup[] GroupedSessions
    {
        get
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var weekStart = today.AddDays(-(int)now.DayOfWeek);

            var filtered = FilteredSessions.ToList();

            var todaySessions = filtered.Where(s => s.UpdatedAt.Date == today).ToList();
            var yesterdaySessions = filtered.Where(s => s.UpdatedAt.Date == yesterday).ToList();
            var weekSessions = filtered.Where(s => s.UpdatedAt >= weekStart && s.UpdatedAt.Date < today).ToList();
            var olderSessions = filtered.Where(s => s.UpdatedAt < weekStart).ToList();

            var groups = new List<SessionGroup>();
            if (todaySessions.Count > 0) groups.Add(new SessionGroup("Сегодня", todaySessions));
            if (yesterdaySessions.Count > 0) groups.Add(new SessionGroup("Вчера", yesterdaySessions));
            if (weekSessions.Count > 0) groups.Add(new SessionGroup("На этой неделе", weekSessions));
            if (olderSessions.Count > 0) groups.Add(new SessionGroup("Ранее", olderSessions));

            return groups.ToArray();
        }
    }

    /// <summary>
    /// Load all sessions from disk
    /// </summary>
    public void LoadAllSessions()
    {
        try
        {
            var dir = _sessionsDir;
            Directory.CreateDirectory(dir);

            var files = Directory.GetFiles(dir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<ChatSession>(json);
                    if (session != null)
                    {
                        _sessions.Add(session);
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
    /// Create a new chat session
    /// </summary>
    public void CreateNew()
    {
        SaveCurrentSession();

        SessionName = "Новый чат";
        ActiveSession = new ChatSession { Name = "Новый чат", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _sessions.Add(ActiveSession!);
    }

    /// <summary>
    /// Save current session to disk
    /// </summary>
    public void SaveCurrentSession(Config config, List<Message> conversationHistory)
    {
        if (ActiveSession == null) return;

        ActiveSession.UpdatedAt = DateTime.UtcNow;
        ActiveSession.Messages.Clear();

        foreach (var msg in conversationHistory)
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

        ActiveSession.Model = config.Model;

        try
        {
            var dir = _sessionsDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{ActiveSession.Id}.json");
            var json = JsonSerializer.Serialize(ActiveSession, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Save current session without messages (just metadata)
    /// </summary>
    public void SaveCurrentSession()
    {
        if (ActiveSession == null) return;
        ActiveSession.UpdatedAt = DateTime.UtcNow;
        try
        {
            var dir = _sessionsDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{ActiveSession.Id}.json");
            var json = JsonSerializer.Serialize(ActiveSession, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Load a chat session by ID
    /// </summary>
    /// <returns>List of messages to restore to ConversationHistory</returns>
    public (ChatSession? session, List<Message> messages) LoadSession(string sessionId)
    {
        // Return messages separately - caller populates ConversationHistory
        var messages = new List<Message>();

        try
        {
            var dir = _sessionsDir;
            var path = Path.Combine(dir, $"{sessionId}.json");

            if (!File.Exists(path)) return (null, messages);

            var json = File.ReadAllText(path);
            var session = JsonSerializer.Deserialize<ChatSession>(json);
            if (session == null) return (null, messages);

            foreach (var msg in session.Messages)
            {
                messages.Add(new Message
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
            }

            return (session, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load session: {ex.Message}");
            return (null, messages);
        }
    }

    /// <summary>
    /// Delete a chat session by ID
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        try
        {
            var dir = _sessionsDir;
            var path = Path.Combine(dir, $"{sessionId}.json");
            if (File.Exists(path)) File.Delete(path);

            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                _sessions.Remove(session);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to delete session: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Export current session to JSON string
    /// </summary>
    public string ExportSessionToJson()
    {
        if (ActiveSession == null) return string.Empty;
        return JsonSerializer.Serialize(ActiveSession, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Export current session to Markdown string
    /// </summary>
    public string ExportSessionToMarkdown()
    {
        if (ActiveSession == null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {ActiveSession.Name}\n");
        sb.AppendLine($"Дата: {ActiveSession.CreatedAt:dd.MM.yyyy HH:mm}\n");
        sb.AppendLine("---\n");

        foreach (var msg in ActiveSession.Messages)
        {
            var role = msg.Role switch
            {
                "user" => "👤 Вы",
                "assistant" => "🤖 Помощник",
                "tool" => "🔧 Результат инструмента",
                _ => msg.Role
            };
            sb.AppendLine($"## {role}\n");
            sb.AppendLine($"{msg.Content}\n");
            sb.AppendLine("---\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Import sessions from JSON string
    /// </summary>
    /// <returns>Number of imported sessions</returns>
    public int ImportSessionsFromJson(string json)
    {
        try
        {
            var sessions = JsonSerializer.Deserialize<List<ChatSession>>(json);
            if (sessions != null)
            {
                int imported = 0;
                foreach (var session in sessions)
                {
                    session.Id = Guid.NewGuid().ToString("N");
                    _sessions.Add(session);
                    imported++;
                }
                return imported;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Import error: {ex.Message}");
        }
        return 0;
    }
}
