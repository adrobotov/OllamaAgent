using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Memory MCP server - provides long-term memory for AI conversations
/// </summary>
public static class McpMemoryTool
{
    private static McpClient? _mcpClient;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialize the memory MCP server
    /// </summary>
    public static async Task<bool> InitializeAsync()
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
                args: new[] { "-y", "@modelcontextprotocol/server-memory" }
            );

            var tools = await mcpClient.ListToolsAsync();
            AppLogger.Logger.Log($"🧠 Memory MCP tools available: {tools.Count}");
            foreach (var tool in tools)
            {
                AppLogger.Logger.Log($"  - {tool.Name}: {tool.Description}");
            }

            lock (_lock)
            {
                _mcpClient = mcpClient;
            }

            AppLogger.Logger.Log("🧠 Memory MCP initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Logger.LogWarning($"⚠️ Memory MCP unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Register memory tools with the executor
    /// </summary>
    public static void Register(ToolExecutor executor)
    {
        // search_memory - search for stored memories
        executor.Register(
            "search_memory",
            "Search stored memories by keyword. Use this to recall past conversations and information.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Search query to find relevant memories"
                    }
                },
                ["required"] = new[] { "query" }
            },
            async args =>
            {
                if (!args.TryGetValue("query", out var qObj) || qObj is not string query)
                    return new ToolResult { Content = "Error: missing required argument: query", IsError = true };

                lock (_lock)
                {
                    if (_mcpClient == null)
                        return new ToolResult { Content = "❌ Memory MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync(
                        "search_memory",
                        new Dictionary<string, object> { ["query"] = query }
                    );

                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Memory search error: {ex.Message}", IsError = true };
                }
            });

        // save_memory - save new memories
        executor.Register(
            "save_memory",
            "Save a new memory for future recall. Use this to store important information from conversations.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["memory"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The memory content to save"
                    }
                },
                ["required"] = new[] { "memory" }
            },
            async args =>
            {
                if (!args.TryGetValue("memory", out var memObj) || memObj is not string memory)
                    return new ToolResult { Content = "Error: missing required argument: memory", IsError = true };

                lock (_lock)
                {
                    if (_mcpClient == null)
                        return new ToolResult { Content = "❌ Memory MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync(
                        "save_memory",
                        new Dictionary<string, object> { ["memory"] = memory }
                    );

                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Memory save error: {ex.Message}", IsError = true };
                }
            });
    }

    private static string FormatResult(JsonElement result)
    {
        if (result.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
        {
            return contentArray[0].GetProperty("text").GetString() ?? string.Empty;
        }
        return result.ToString();
    }

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
