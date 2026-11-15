using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaAgent.Tools;

/// <summary>
/// MCP client that communicates with an MCP server via stdio using JSON-RPC 2.0
/// </summary>
public class McpClient : IDisposable
{
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _nextRequestId = 1;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly object _lock = new();
    private bool _initialized;
    private bool _disposed;
    private System.Threading.Timer? _heartbeatTimer;

    /// <summary>
    /// Initialize the MCP session
    /// </summary>
    public async Task InitializeAsync(string command, string[] args, Dictionary<string, string>? env = null, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McpClient));

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (env != null)
        {
            foreach (var kvp in env)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }
        }

        _process = new Process { StartInfo = psi };
        _process.Start();

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;

        // Start reading responses asynchronously
        _ = Task.Run(() => ReadResponsesAsync(ct));

        // Send initialize request
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = IncrementId(),
            @method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "OllamaAgent",
                    version = "1.0.0"
                }
            }
        };

        var response = await SendRequestAsync(initRequest, ct);

        // Send initialized notification
        var notification = new
        {
            jsonrpc = "2.0",
            @method = "notifications/initialized"
        };
        await SendNotificationAsync(notification);

        _initialized = true;
    }

    /// <summary>
    /// List available tools from the MCP server
    /// </summary>
    public async Task<List<McpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        if (!_initialized) throw new InvalidOperationException("MCP client not initialized");

        var request = new
        {
            jsonrpc = "2.0",
            id = IncrementId(),
            @method = "tools/list",
            @params = (object?)null
        };

        var response = await SendRequestAsync(request, ct);

        var tools = new List<McpTool>();
        if (response.TryGetProperty("result", out var result) &&
            result.TryGetProperty("tools", out var toolsArray))
        {
            foreach (var tool in toolsArray.EnumerateArray())
            {
                tools.Add(new McpTool
                {
                    Name = tool.GetProperty("name").GetString() ?? string.Empty,
                    Description = tool.GetProperty("description").GetString() ?? string.Empty,
                    InputSchema = tool.TryGetProperty("inputSchema", out var schema) ? schema : tool.GetProperty("parameters")
                });
            }
        }

        return tools;
    }

    /// <summary>
    /// Call a tool on the MCP server
    /// </summary>
    public async Task<JsonElement> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken ct = default)
    {
        if (!_initialized) throw new InvalidOperationException("MCP client not initialized");

        var request = new
        {
            jsonrpc = "2.0",
            id = IncrementId(),
            @method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments
            }
        };

        var response = await SendRequestAsync(request, ct);

        if (response.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
            throw new Exception($"MCP tool error: {message}");
        }

        return response.GetProperty("result");
    }

    private async Task ReadResponsesAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                var line = await _stdout?.ReadLineAsync() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                    {
                        var id = idProp.GetInt32();
                        TaskCompletionSource<JsonElement>? tcs;
                        lock (_lock)
                        {
                            _pendingRequests.TryGetValue(id, out tcs);
                            if (tcs != null) _pendingRequests.Remove(id);
                        }

                        tcs?.TrySetResult(root);
                    }
                }
                catch
                {
                    // Ignore parse errors for notifications
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MCP read error: {ex.Message}");
        }
    }

    private int IncrementId()
    {
        lock (_lock)
        {
            return _nextRequestId++;
        }
    }

    private async Task<JsonElement> SendRequestAsync(object request, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<JsonElement>();

        int id;
        lock (_lock)
        {
            id = IncrementId();
        }

        // Serialize with correct ID
        var reqDict = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id
        };
        
        // Extract method and params from request
        var jsonStr = JsonSerializer.Serialize(request);
        var reqObj = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonStr) ?? new();
        if (reqObj.TryGetValue("method", out var method)) reqDict["method"] = method;
        if (reqObj.TryGetValue("params", out var paramsObj)) reqDict["params"] = paramsObj;
        
        var json = JsonSerializer.Serialize(reqDict);

        lock (_lock)
        {
            _pendingRequests[id] = tcs;
        }

        await _stdin!.WriteLineAsync(json);
        await _stdin.FlushAsync();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            var result = await tcs.Task.WaitAsync(cts.Token);
            return result;
        }
        catch (TimeoutException)
        {
            throw new Exception("MCP request timed out");
        }
    }

    private async Task SendNotificationAsync(object notification)
    {
        var json = JsonSerializer.Serialize(notification);
        await _stdin!.WriteLineAsync(json);
        await _stdin.FlushAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _heartbeatTimer?.Dispose();

        // Send shutdown
        var shutdown = new { jsonrpc = "2.0", id = IncrementId(), @method = "shutdown" };
        _ = SendRequestAsync(shutdown, CancellationToken.None).ContinueWith(_ =>
        {
            var exit = new { jsonrpc = "2.0", @method = "notifications/exit" };
            _ = SendNotificationAsync(exit);
        });

        _stdin?.Close();
        _stdout?.Close();

        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.WaitForExit();
        }

        _process?.Dispose();
    }
}

/// <summary>
/// MCP tool definition
/// </summary>
public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement InputSchema { get; set; }
}
