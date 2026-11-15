using System;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Simple logging interface for decoupling from Console.WriteLine
/// </summary>
public interface ILogger
{
    void Log(string message);
    void LogError(string message);
    void LogWarning(string message);
}

/// <summary>
/// Console-based logger implementation
/// </summary>
public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogError(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }
}
