using Avalonia;
using System;

namespace OllamaAgent;

/// <summary>
/// Application entry point
/// </summary>
public class Program
{
    // Initialization code. Don't insert any runtime code here
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
