using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaAgent.Models;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Filesystem MCP server - provides extended filesystem operations
/// </summary>
public static class McpFilesystemTool
{
    private static McpClient? _mcpClient;
    private static readonly object _lock = new();
    private static readonly List<string> _allowedDirs = new();

    /// <summary>
    /// Initialize the filesystem MCP server
    /// </summary>
    public static async Task<bool> InitializeAsync(List<string>? allowedDirs = null)
    {
        lock (_lock)
        {
            if (_mcpClient != null) return true;
        }

        _allowedDirs.Clear();
        if (allowedDirs != null)
            _allowedDirs.AddRange(allowedDirs);

        try
        {
            var mcpClient = new McpClient();
            await mcpClient.InitializeAsync(
                command: "npx",
                args: new[] { "-y", "@modelcontextprotocol/server-filesystem", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..") },
                env: new Dictionary<string, string>()
            );

            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"📁 Filesystem MCP tools available: {tools.Count}");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }

            lock (_lock)
            {
                _mcpClient = mcpClient;
            }

            Console.WriteLine("📁 Filesystem MCP initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Filesystem MCP unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Register filesystem tools with the executor
    /// </summary>
    public static void Register(ToolExecutor executor)
    {
        // read_file - read file content
        executor.Register(
            "fs_read_file",
            "Read the full content of a file from the filesystem.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the file to read"
                    }
                },
                ["required"] = new[] { "path" }
            },
            async args =>
            {
                if (!args.TryGetValue("path", out var pObj) || pObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };

                lock (_lock)
                {
                    if (_mcpClient == null)
                        return new ToolResult { Content = "❌ Filesystem MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync("read_file", new Dictionary<string, object> { ["path"] = path });
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Read error: {ex.Message}", IsError = true };
                }
            });

        // write_file - write content to file
        executor.Register(
            "fs_write_file",
            "Write content to a file, creating it if it doesn't exist.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the file to write"
                    },
                    ["content"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Content to write to the file"
                    }
                },
                ["required"] = new[] { "path", "content" }
            },
            async args =>
            {
                if (!args.TryGetValue("path", out var pObj) || pObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };
                if (!args.TryGetValue("content", out var cObj) || cObj is not string content)
                    return new ToolResult { Content = "Error: missing required argument: content", IsError = true };

                lock (_lock)
                {
                    if (_mcpClient == null)
                        return new ToolResult { Content = "❌ Filesystem MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync(
                        "write_file",
                        new Dictionary<string, object> { ["path"] = path, ["content"] = content }
                    );
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ Write error: {ex.Message}", IsError = true };
                }
            });

        // list_directory - list directory contents
        executor.Register(
            "fs_list_directory",
            "List the contents of a directory.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the directory to list"
                    }
                },
                ["required"] = new[] { "path" }
            },
            async args =>
            {
                if (!args.TryGetValue("path", out var pObj) || pObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };

                lock (_lock)
                {
                    if (_mcpClient == null)
                        return new ToolResult { Content = "❌ Filesystem MCP not initialized", IsError = true };
                }

                try
                {
                    var result = await _mcpClient!.CallToolAsync(
                        "list_directory",
                        new Dictionary<string, object> { ["path"] = path }
                    );
                    return new ToolResult { Content = FormatResult(result) };
                }
                catch (Exception ex)
                {
                    return new ToolResult { Content = $"❌ List error: {ex.Message}", IsError = true };
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
