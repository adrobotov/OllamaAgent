using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OllamaAgent.ViewModels;
using System;
using System.IO;

namespace OllamaAgent.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+Enter to send
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            var viewModel = DataContext as ChatViewModel;
            viewModel?.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RefreshModels_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        viewModel?.LoadModelsAsync();
    }

    private void SaveConfig_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        var configPath = GetConfigPath();
        viewModel?.SaveConfigCommand.Execute(configPath);
    }

    private void LoadConfig_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        var configPath = GetConfigPath();
        viewModel?.LoadConfigCommand.Execute(configPath);
    }

    private string GetConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(home, ".ollama-agent");
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, "config.json");
    }
}
