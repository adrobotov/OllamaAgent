using System;
using System.IO;

namespace OllamaAgent.Models;

/// <summary>
/// Centralized application path helpers
/// </summary>
public static class ConfigPaths
{
    private static string? _configDir;
    private static string? _sessionsDir;

    /// <summary>
    /// Directory for user configuration (~/.ollama-agent/)
    /// </summary>
    public static string ConfigDir
    {
        get
        {
            if (_configDir == null)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _configDir = Path.Combine(home, ".ollama-agent");
                Directory.CreateDirectory(_configDir);
            }
            return _configDir;
        }
    }

    /// <summary>
    /// Full path to config.json
    /// </summary>
    public static string ConfigFile => Path.Combine(ConfigDir, "config.json");

    /// <summary>
    /// Directory for chat sessions (app data / sessions/)
    /// </summary>
    public static string SessionsDir
    {
        get
        {
            if (_sessionsDir == null)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _sessionsDir = Path.Combine(appData, "OllamaAgent", "sessions");
                Directory.CreateDirectory(_sessionsDir);
            }
            return _sessionsDir;
        }
    }
}
