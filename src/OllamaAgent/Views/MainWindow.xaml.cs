using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OllamaAgent.Models;
using OllamaAgent.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        else if (e.PropertyName == nameof(ChatViewModel.HasPendingConfirmations))
        {
            // Auto-scroll when tool confirmations appear/disappear
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(ScrollToBottom, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void OnChatMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll on new messages
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            // Use Dispatcher to ensure UI is updated before scrolling
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(ScrollToBottom, Avalonia.Threading.DispatcherPriority.Background);
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

    private void CollapseToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChatMessageViewModel vm)
        {
            vm.ToggleCollapse();
        }
    }

    private void UpdateToolResultButtons()
    {
        // This is handled by the ItemsControl template binding
    }

    private void CopyMessage_Click(object? sender, RoutedEventArgs e)
    {
        ChatMessageViewModel? vm = null;
        
        // Handle both button and menu item clicks
        if (sender is Button btn)
        {
            vm = btn.Tag as ChatMessageViewModel;
        }
        else if (sender is MenuItem menuItem && menuItem.DataContext is ChatMessageViewModel menuVm)
        {
            vm = menuVm;
        }
        
        if (vm != null)
        {
            // Copy to clipboard using Avalonia's clipboard
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                _ = topLevel.Clipboard.SetTextAsync(vm.Content);
            }
            
            // Update status
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var viewModel = DataContext as ChatViewModel;
                viewModel?.SetStatus("✅ Скопировано");
            });
        }
    }

    private void NewChat_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        viewModel?.NewChatCommand.Execute(null);
    }

    private void SessionItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ChatSession session)
        {
            // Deselect all
            foreach (var s in (DataContext as ChatViewModel)?.ChatSessions ?? new ObservableCollection<ChatSession>())
            {
                s.IsSelected = false;
            }
            
            // Select this one
            session.IsSelected = true;
            
            // Load the session
            var viewModel = DataContext as ChatViewModel;
            viewModel?.LoadSession(session.Id);
        }
    }

    private void DeleteChat_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel?.ActiveSession != null)
        {
            viewModel.DeleteChatCommand.Execute(viewModel.ActiveSession.Id);
        }
    }

    private async void AttachImage_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите изображения",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });
            
            if (files.Count > 0)
            {
                var filePaths = files.Select(f => f.Path.IsAbsoluteUri 
                    ? f.Path.LocalPath 
                    : f.Path.ToString()).ToList();
                await viewModel.AttachImagesAsync(filePaths);
            }
        }
    }

    private void RemoveImage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ImagePreviewInfo preview)
        {
            var viewModel = DataContext as ChatViewModel;
            viewModel?.RemoveImage(preview);
        }
    }

    private void ApproveTool_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ToolConfirmation confirmation)
        {
            confirmation.IsApproved = true;
            confirmation.CompletionSource.TrySetResult(true);
            var viewModel = DataContext as ChatViewModel;
            viewModel?.RemoveFromPendingConfirmations(confirmation);
        }
    }

    private void RejectTool_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ToolConfirmation confirmation)
        {
            confirmation.IsRejected = true;
            confirmation.CompletionSource.TrySetResult(false);
            var viewModel = DataContext as ChatViewModel;
            viewModel?.RemoveFromPendingConfirmations(confirmation);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Hide window instead of closing (minimize to tray)
        e.Cancel = true;
        this.Hide();
    }

    /// <summary>
    /// Minimize window to tray (hide it)
    /// </summary>
    private void MinimizeToTray_Click(object? sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    /// <summary>
    /// Close the application completely
    /// </summary>
    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        // Dispose ViewModel resources
        if (DataContext is ChatViewModel vm)
        {
            vm.Dispose();
        }
        
        this.Close();
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        // Ctrl+N — new chat
        if (e.Key == Key.N && e.KeyModifiers == KeyModifiers.Control)
        {
            viewModel.NewChatCommand.Execute(null);
            e.Handled = true;
        }
        // Ctrl+D — delete chat
        else if (e.Key == Key.D && e.KeyModifiers == KeyModifiers.Control)
        {
            DeleteChat_Click(null, null);
            e.Handled = true;
        }
        // Ctrl+S — save config
        else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            SaveConfig_Click(null, null);
            e.Handled = true;
        }
        // Ctrl+Shift+S — export JSON
        else if (e.Key == Key.S && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            ExportJson_Click(null, null);
            e.Handled = true;
        }
    }

    private async void ExportJson_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        var json = viewModel.ExportSessionToJson();
        if (string.IsNullOrEmpty(json)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт чата",
                DefaultExtension = "json",
                SuggestedFileName = "chat.json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });
            
            if (file != null)
            {
                File.WriteAllText(file.Path.LocalPath, json);
                viewModel.SetStatus($"✅ Экспортировано: {file.Path.LocalPath}");
            }
        }
    }

    private async void ExportMarkdown_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        var markdown = viewModel.ExportSessionToMarkdown();
        if (string.IsNullOrEmpty(markdown)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт чата",
                DefaultExtension = "md",
                SuggestedFileName = "chat.md",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Markdown Files") { Patterns = new[] { "*.md" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });
            
            if (file != null)
            {
                File.WriteAllText(file.Path.LocalPath, markdown);
                viewModel.SetStatus($"✅ Экспортировано в Markdown: {file.Path.LocalPath}");
            }
        }
    }

    private async void ImportJson_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ChatViewModel;
        if (viewModel == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Импорт чатов",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });
            
            if (files.Count > 0)
            {
                int imported = 0;
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file.Path.LocalPath);
                        imported += viewModel.ImportSessionsFromJson(json);
                    }
                    catch
                    {
                        // Skip invalid files
                    }
                }
                if (imported == 0)
                {
                    viewModel.SetStatus("⚠️ Не удалось импортировать сессии");
                }
            }
        }
    }
}
