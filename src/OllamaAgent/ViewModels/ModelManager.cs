using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Services;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Manages available models, model selection, and model capability detection.
/// </summary>
public partial class ModelManager
{
    private readonly OllamaClient _ollamaClient;
    private readonly Config _config;
    private readonly ILogger _logger;

    public ModelManager(OllamaClient ollamaClient, Config config, ILogger logger)
    {
        _ollamaClient = ollamaClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// List of available models from Ollama
    /// </summary>
    public List<ModelInfo> AvailableModels { get; set; } = new();

    /// <summary>
    /// Observable collection of model display names for UI binding
    /// </summary>
    public ObservableCollection<string> ModelOptions { get; set; } = new();

    /// <summary>
    /// Currently selected model display name
    /// </summary>
    public string SelectedModelDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Load available models from Ollama
    /// </summary>
    public async Task LoadModelsAsync(Action<string> setStatus)
    {
        try
        {
            var models = await _ollamaClient.ListModelsAsync();

            if (models.Count == 0)
            {
                setStatus("⚠️ Модели не найдены. Установите: ollama pull llama3.2");
                return;
            }

            AvailableModels = models;
            ModelOptions = new ObservableCollection<string>(models.Select(m => m.DisplayName));

            // Select current model if available
            if (!string.IsNullOrEmpty(_config.Model) && models.Any(m => m.Name == _config.Model))
            {
                SelectedModelDisplay = models.First(m => m.Name == _config.Model).DisplayName;
            }
            else if (models.Count > 0)
            {
                SelectedModelDisplay = models[0].DisplayName;
                _config.Model = models[0].Name;
            }

            var capSet = new HashSet<string>(models.SelectMany(m => m.Capabilities));
            setStatus($"📦 {models.Count} моделей | Доступно: {string.Join(", ", capSet)}");
        }
        catch (Exception ex)
        {
            setStatus($"⚠️ Ошибка загрузки моделей: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle model selection change - update config
    /// </summary>
    public void OnSelectedModelDisplayChanged(string value, Action<string> setStatus)
    {
        try
        {
            // Find matching model and update config
            var model = AvailableModels.FirstOrDefault(m => m.DisplayName == value);
            if (model != null)
            {
                _config.Model = model.Name;
                _logger.Log($"🔄 Model changed to: {model.Name}");
            }
            else if (!string.IsNullOrEmpty(value))
            {
                // If no match found, use the display name as model name
                _config.Model = value;
                _logger.LogWarning($"⚠️ Model display name not found in list, using: {value}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error changing model: {ex.Message}");
            setStatus($"❌ Ошибка смены модели: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if a model supports tool calling based on its name/family
    /// </summary>
    public static bool ModelSupportsTools(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;

        var nameLower = modelName.ToLower();

        // Reasoning models DON'T support tool calling
        var reasoningModels = new[] { "deepseek-r1", "qwq", "r1", "qwq-32b" };
        foreach (var name in reasoningModels)
        {
            if (nameLower.Contains(name)) return false;
        }

        // Models known to support tools (specific versions only)
        var toolSupportingFamilies = new[] { "llama", "gemma", "mistral", "mixtral", "qwen", "codellama", "codegemma" };
        var toolSupportingNames = new[] { "llama3.1", "llama3.2", "gemma2", "gemma3", "gemma4", "mistral-large", "qwen2.5", "qwen3" };

        foreach (var family in toolSupportingFamilies)
        {
            if (nameLower.Contains(family)) return true;
        }

        foreach (var name in toolSupportingNames)
        {
            if (nameLower.Contains(name)) return true;
        }

        // Models known NOT to support tools
        var noToolNames = new[] { "saiga", "falcon", "stable-code", "tinyllama", "deepseek", "qwq" };
        foreach (var name in noToolNames)
        {
            if (nameLower.Contains(name)) return false;
        }

        // Default: assume no tool support for unknown models
        return false;
    }

    /// <summary>
    /// Check if a model supports thinking/reasoning mode
    /// </summary>
    public static bool ModelSupportsThinking(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;

        var nameLower = modelName.ToLower();

        // Only specific models support thinking
        var thinkingModels = new[] { "deepseek-r1", "qwq", "qwq-32b" };
        foreach (var name in thinkingModels)
        {
            if (nameLower.Contains(name)) return true;
        }

        return false;
    }
}
