using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Weather tool that uses MCP (Model Context Protocol) to call weatherapi-mcp server
/// </summary>
public static class McpWeatherTool
{
    private static McpClient? _mcpClient;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialize the MCP weather tool
    /// </summary>
    public static async Task<bool> InitializeAsync(string apiKey)
    {
        lock (_lock)
        {
            if (_mcpClient != null) return true;
        }

        try
        {
            var mcpClient = new McpClient();
            await mcpClient.InitializeAsync(
                command: "npx",
                args: new[] { "-y", "weatherapi-mcp" },
                env: new Dictionary<string, string>
                {
                    ["WEATHERAPI_KEY"] = apiKey
                }
            );

            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"🔧 MCP tools available: {tools.Count}");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }

            lock (_lock)
            {
                _mcpClient = mcpClient;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to initialize MCP weather tool: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Register the weather tool with the executor
    /// </summary>
    public static void Register(ToolExecutor executor)
    {
        executor.Register(
            "get_weather",
            "Get current weather conditions for a location using MCP weather server.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "City name or coordinates (e.g., 'Moscow', 'London', '40.7128,-74.0060')"
                    }
                },
                ["required"] = new[] { "location" }
            },
            async args =>
            {
                if (!args.TryGetValue("location", out var locObj) || locObj is not string location)
                {
                    return new ToolResult
                    {
                        Content = "Error: missing required argument: location",
                        IsError = true
                    };
                }

                lock (_lock)
                {
                    if (_mcpClient == null)
                    {
                        return new ToolResult
                        {
                            Content = "❌ MCP weather server not initialized. Check API key configuration.",
                            IsError = true
                        };
                    }
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync(
                        "get_weather",
                        new Dictionary<string, object> { ["location"] = location }
                    );

                    if (result.TryGetProperty("content", out var contentArray) &&
                        contentArray.GetArrayLength() > 0)
                    {
                        var text = contentArray[0].GetProperty("text").GetString() ?? string.Empty;
                        return new ToolResult { Content = text };
                    }

                    return new ToolResult
                    {
                        Content = "No weather data returned from MCP server",
                        IsError = true
                    };
                }
                catch (Exception ex)
                {
                    return new ToolResult
                    {
                        Content = $"❌ Error fetching weather via MCP: {ex.Message}",
                        IsError = true
                    };
                }
            });
    }

    /// <summary>
    /// Dispose the MCP client
    /// </summary>
    public static void Dispose()
    {
        McpClient? client;
        lock (_lock)
        {
            client = _mcpClient;
            _mcpClient = null;
        }
        client?.Dispose();
    }
}
