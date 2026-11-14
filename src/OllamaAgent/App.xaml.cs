using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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

    private ChatViewModel? _viewModel;

    public override void OnFrameworkInitializationCompleted()
    {
        // Bind ViewModel to View
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validation from both Avalonia and CommunityToolkit
            // See: https://www.meziantou.net/duplicate-validation-in-avalonia-and-community.toolkit.mvvm.htm
            DisableAvaloniaDataAnnotationValidation();

            var config = LoadConfig();
            _viewModel = new ChatViewModel(config);
            
            var mainWindow = new MainWindow
            {
                DataContext = _viewModel
            };

            // Apply theme from config
            ApplyTheme(desktop, config.Theme);

            desktop.MainWindow = mainWindow;

            // Load models asynchronously on startup
            _ = _viewModel.LoadModelsAsync();

            // Dispose ViewModel on shutdown
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _viewModel?.Dispose();
    }

    private static void ApplyTheme(IClassicDesktopStyleApplicationLifetime desktop, string theme)
    {
        if (string.IsNullOrEmpty(theme) || theme == "system")
        {
            // Use system theme (default)
            return;
        }

        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        // Apply theme variant to the window
        if (desktop.MainWindow is Window window)
        {
            window.RequestedThemeVariant = variant;
        }
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
