using System;
using OllamaAgent.ViewModels;

namespace OllamaAgent.Tools;

/// <summary>
/// Global logger accessible from static tool classes
/// Set once during app initialization via ChatViewModel
/// </summary>
public static class AppLogger
{
    private static ILogger? _instance;

    public static ILogger Logger
    {
        get => _instance ??= new ConsoleLogger();
        internal set => _instance = value;
    }
}
