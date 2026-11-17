using System.Collections.Generic;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Preview information for an attached image.
/// </summary>
public class ImagePreviewInfo
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
}
