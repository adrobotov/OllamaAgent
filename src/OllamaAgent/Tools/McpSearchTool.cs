using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Brave Search MCP server - provides web search capabilities
/// </summary>
public static class McpSearchTool
{
    private static McpClient? _mcpClient;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialize the Brave Search MCP server
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
                args: new[] { "-y", "@modelcontextprotocol/server-brave-search" },
                env: new Dictionary<string, string>
                {
                    ["BRAVE_API_KEY"] = apiKey
                }
            );

            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"🔍 Search MCP tools available: {tools.Count}");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }

            lock (_lock)
            {
                _mcpClient = mcpClient;
            }

            Console.WriteLine("🔍 Brave Search MCP initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Brave Search MCP unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Register search tools with the executor
    /// </summary>
    public static void Register(ToolExecutor executor)
    {
        // brave_search - search the web
        executor.Register(
            "brave_search",
            "Search the web using Brave Search engine. Returns search results with titles, snippets, and URLs.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The search query to look up"
                    },
                    ["count"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Number of results to return (1-20, default 10)"
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
                        return new ToolResult { Content = "❌ Brave Search MCP not initialized. Set BRAVE_API_KEY in config.", IsError = true };
                }

                var searchArgs = new Dictionary<string, object> { ["query"] = query };
                if (args.TryGetValue("count", out var countObj) && countObj is int count)
                    searchArgs["count"] = count;

                try
                {
                    var result = await _mcpClient!.CallToolAsync("brave_search", searchArgs);
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Search error: {ex.Message}", IsError = true };
                }
            });

        // brave_local_search - search locally
        executor.Register(
            "brave_local_search",
            "Search for local businesses and places using Brave Search.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The local search query (e.g., 'restaurants in Moscow')"
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
                        return new ToolResult { Content = "❌ Brave Search MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync("brave_local_search", new Dictionary<string, object> { ["query"] = query });
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Local search error: {ex.Message}", IsError = true };
                }
            });
    }

    private static string FormatResult(JsonElement result)
    {
        if (result.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
        {
            var texts = new List<string>();
            foreach (var item in contentArray.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text))
                    texts.Add(text.GetString() ?? string.Empty);
            }
            return string.Join("\n\n", texts);
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
