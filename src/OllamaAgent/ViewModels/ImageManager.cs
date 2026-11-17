using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Manages image attachments for the current message.
/// </summary>
public partial class ImageManager : ObservableObject
{
    /// <summary>
    /// Preview information for attached images
    /// </summary>
    public List<ImagePreviewInfo> ImagePreviews { get; } = new();

    /// <summary>
    /// Base64-encoded images for the current message
    /// </summary>
    private List<string> _attachedImages { get; } = new();

    /// <summary>
    /// Whether images are attached to the current message
    /// </summary>
    [ObservableProperty]
    private bool _hasImages;

    /// <summary>
    /// Status text for image operations
    /// </summary>
    public string ImageStatusText { get; private set; } = string.Empty;

    /// <summary>
    /// Attach images to the current message
    /// </summary>
    public async Task AttachImagesAsync(List<string> filePaths, Action<string> setStatus)
    {
        try
        {
            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;

                var bytes = await File.ReadAllBytesAsync(filePath);
                var base64 = Convert.ToBase64String(bytes);
                _attachedImages.Add(base64);

                var fileName = Path.GetFileName(filePath);
                var size = bytes.Length;
                ImagePreviews.Add(new ImagePreviewInfo { Name = fileName, Size = size });
            }

            HasImages = _attachedImages.Count > 0;
            ImageStatusText = $"📷 Прикреплено изображений: {_attachedImages.Count}";
            setStatus(ImageStatusText);
        }
        catch (Exception ex)
        {
            ImageStatusText = $"❌ Ошибка загрузки изображений: {ex.Message}";
            setStatus(ImageStatusText);
        }
    }

    /// <summary>
    /// Remove an attached image
    /// </summary>
    public void RemoveImage(ImagePreviewInfo preview, Action<string> setStatus)
    {
        var index = ImagePreviews.IndexOf(preview);
        if (index >= 0)
        {
            _attachedImages.RemoveAt(index);
            ImagePreviews.Remove(preview);
            HasImages = _attachedImages.Count > 0;
            ImageStatusText = $"📷 Прикреплено изображений: {_attachedImages.Count}";
            setStatus(ImageStatusText);
        }
    }

    /// <summary>
    /// Get attached images and clear the list
    /// Used when sending a message to retrieve and reset images
    /// </summary>
    public List<string> GetAndClearImages()
    {
        var images = new List<string>(_attachedImages);
        _attachedImages.Clear();
        ImagePreviews.Clear();
        HasImages = false;
        return images;
    }

    /// <summary>
    /// Clear all attached images
    /// </summary>
    public void ClearImages()
    {
        _attachedImages.Clear();
        ImagePreviews.Clear();
        HasImages = false;
    }
}
