using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OllamaAgent.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace OllamaAgent.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private ScrollViewer? _chatScrollViewer;
    private ObservableCollection<ChatMessageViewModel>? _chatMessages;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Find the chat ScrollViewer
        _chatScrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");
        
        // Subscribe to ViewModel changes
        if (DataContext is ChatViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            
            // Subscribe to chat messages changes for auto-scroll
            _chatMessages = vm.ChatMessages;
            _chatMessages.CollectionChanged += OnChatMessagesChanged;
            
            // Scroll to bottom initially
            ScrollToBottom();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        if (_chatMessages != null)
        {
            _chatMessages.CollectionChanged -= OnChatMessagesChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.ShowSettings))
        {
            // Settings panel visibility is bound, no action needed
        }
    }

    private void OnChatMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll on new messages
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        if (_chatScrollViewer != null)
        {
            _chatScrollViewer.ScrollToEnd();
        }
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

    private async void RefreshModels_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel != null)
        {
            await viewModel.LoadModelsAsync();
        }
    }

    private async void SaveConfig_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить конфигурацию",
                DefaultExtension = "json",
                SuggestedFileName = "config.json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });
            
            if (file != null)
            {
                var path = file.Path.IsAbsoluteUri 
                    ? file.Path.LocalPath 
                    : file.Path.ToString();
                viewModel.SaveConfigCommand.Execute(path);
            }
        }
        else
        {
            // Fallback: save to default location
            var configPath = GetConfigPath();
            viewModel.SaveConfigCommand.Execute(configPath);
        }
    }

    private async void LoadConfig_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Загрузить конфигурацию",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });
            
            if (files.Count > 0)
            {
                var file = files[0];
                var path = file.Path.IsAbsoluteUri 
                    ? file.Path.LocalPath 
                    : file.Path.ToString();
                viewModel.LoadConfigCommand.Execute(path);
            }
        }
    }

    private string GetConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(home, ".ollama-agent");
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, "config.json");
    }

    private void ThemeToggleBtn_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel != null)
        {
            viewModel.ToggleThemeCommand.Execute(null);
            
            // Update icon based on current theme
            UpdateThemeIcon(viewModel);
        }
    }

    private void UpdateThemeIcon(ChatViewModel viewModel)
    {
        if (ThemeToggleBtn != null)
        {
            ThemeToggleBtn.Content = viewModel.Config.Theme switch
            {
                "dark" => "☀️",
                "light" => "🌙",
                _ => "💻" // system
            };
        }
    }
}
