using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;
using OllamaAgent.Models;
using OllamaAgent.ViewModels;
using OllamaAgent.Views;

namespace OllamaAgent;

/// <summary>
/// Application entry point
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Bind ViewModel to View
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validation from both Avalonia and CommunityToolkit
            // See: https://www.meziantou.net/duplicate-validation-in-avalonia-and-community.toolkit.mvvm.htm
            DisableAvaloniaDataAnnotationValidation();

            var config = LoadConfig();
            var viewModel = new ChatViewModel(config);
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            // Load models asynchronously on startup
            _ = viewModel.LoadModelsAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Config LoadConfig()
    {
        var configPath = GetConfigPath();
        
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<Config>(json);
                if (config != null) return config;
            }
            catch
            {
                // Fall back to defaults
            }
        }

        return new Config
        {
            ChatFile = Path.Combine(Path.GetDirectoryName(configPath) ?? ".", "chat.json")
        };
    }

    private string GetConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(home, ".ollama-agent");
        return Path.Combine(configDir, "config.json");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Data validation plugins are handled by CommunityToolkit.Mvvm
        // This method is kept for future customization
    }
}
