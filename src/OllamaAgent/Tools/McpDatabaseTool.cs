using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Database MCP server - provides database operations for PostgreSQL, MySQL, SQLite
/// </summary>
public static class McpDatabaseTool
{
    private static McpClient? _mcpClient;
    private static readonly object _lock = new();

    /// <summary>
    /// Initialize the database MCP server
    /// </summary>
    public static async Task<bool> InitializeAsync(Dictionary<string, string> connectionStrings)
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
                args: new[] { "-y", "@modelcontextprotocol/server-postgres", "postgresql://localhost" },
                env: connectionStrings
            );

            var tools = await mcpClient.ListToolsAsync();
            AppLogger.Logger.Log($"🗄️ Database MCP tools available: {tools.Count}");
            foreach (var tool in tools)
            {
                AppLogger.Logger.Log($"  - {tool.Name}: {tool.Description}");
            }

            lock (_lock)
            {
                _mcpClient = mcpClient;
            }

            AppLogger.Logger.Log("🗄️ Database MCP initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Logger.LogWarning($"⚠️ Database MCP unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Register database tools with the executor
    /// </summary>
    public static void Register(ToolExecutor executor)
    {
        // query - execute SQL query
        executor.Register(
            "db_query",
            "Execute a read-only SQL query against the connected database.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "SQL query to execute (SELECT only)"
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
                        return new ToolResult { Content = "❌ Database MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync(
                        "query",
                        new Dictionary<string, object> { ["query"] = query }
                    );
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Query error: {ex.Message}", IsError = true };
                }
            });

        // read_query - read data from database
        executor.Register(
            "db_read",
            "Read data from database tables. Safe read-only operation.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["table"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Table name to read from"
                    },
                    ["limit"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum rows to return (default 100)"
                    }
                },
                ["required"] = new[] { "table" }
            },
            async args =>
            {
                if (!args.TryGetValue("table", out var tObj) || tObj is not string table)
                    return new ToolResult { Content = "Error: missing required argument: table", IsError = true };

                lock (_lock)
                {
                    if (_mcpClient == null)
                        return new ToolResult { Content = "❌ Database MCP not initialized", IsError = true };
                }

                var dbArgs = new Dictionary<string, object> { ["table"] = table };
                if (args.TryGetValue("limit", out var lObj) && lObj is int limit)
                    dbArgs["limit"] = limit;

                try
                {
                    var result = await _mcpClient!.CallToolAsync("read_query", dbArgs);
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Read error: {ex.Message}", IsError = true };
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
