using System.Threading.Tasks;

namespace OllamaAgent.ViewModels;

/// <summary>
/// Represents a pending tool execution confirmation request.
/// </summary>
public class ToolConfirmation
{
    public string ToolName { get; set; } = string.Empty;
    public string ToolNameRu { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    private bool _isRejected;
    public bool IsRejected 
    { 
        get => _isRejected; 
        set 
        {
            _isRejected = value;
        }
    }
    
    /// <summary>
    /// Whether the tool was approved (inverse of IsRejected)
    /// </summary>
    public bool IsApproved 
    { 
        get => !_isRejected; 
        set => _isRejected = !value; 
    }
    
    public TaskCompletionSource<bool> CompletionSource { get; } = new();
}
