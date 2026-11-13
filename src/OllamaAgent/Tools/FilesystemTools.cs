using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Filesystem-related tools
/// </summary>
public static class FilesystemTools
{
    public static void Register(ToolExecutor executor, string rootDir)
    {
        // read_file: Read file contents
        executor.Register(
            "read_file",
            "Read the contents of a file. Returns the file content as a string.",
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
                if (!args.TryGetValue("path", out var pathObj) || pathObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };

                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path);
                
                if (!File.Exists(fullPath))
                    return new ToolResult { Content = $"Error: file not found: {fullPath}", IsError = true };

                var content = await File.ReadAllTextAsync(fullPath);
                
                if (content.Length > 10000)
                    content = content.Substring(0, 10000) + "\n... (truncated)";

                return new ToolResult { Content = content };
            });

        // write_file: Write content to a file
        executor.Register(
            "write_file",
            "Write content to a file. Creates the file if it doesn't exist, or overwrites if it does.",
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
                if (!args.TryGetValue("path", out var pathObj) || pathObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };

                if (!args.TryGetValue("content", out var contentObj) || contentObj is not string content)
                    return new ToolResult { Content = "Error: missing required argument: content", IsError = true };

                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path);
                var directory = Path.GetDirectoryName(fullPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllTextAsync(fullPath, content);
                return new ToolResult { Content = $"Successfully wrote {content.Length} bytes to {fullPath}" };
            });

        // list_directory: List directory contents
        executor.Register(
            "list_directory",
            "List the contents of a directory.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the directory (default: current directory)"
                    }
                }
            },
            async args =>
            {
                var path = ".";
                if (args.TryGetValue("path", out var pathObj) && pathObj is string p)
                    path = p;

                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path);
                
                if (!Directory.Exists(fullPath))
                    return new ToolResult { Content = $"Error: directory not found: {fullPath}", IsError = true };

                var sb = new StringBuilder();
                sb.AppendLine($"Contents of {fullPath}:");
                sb.AppendLine();

                foreach (var dir in Directory.GetDirectories(fullPath))
                    sb.AppendLine($"[DIR]  {Path.GetFileName(dir)}/");

                foreach (var file in Directory.GetFiles(fullPath))
                    sb.AppendLine($"[FILE] {Path.GetFileName(file)}");

                var result = sb.ToString();
                if (result.Length > 10000)
                    result = result.Substring(0, 10000) + "\n... (truncated)";

                return new ToolResult { Content = result };
            });

        // create_directory: Create a new directory
        executor.Register(
            "create_directory",
            "Create a new directory and any intermediate directories that don't exist.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the directory to create"
                    }
                },
                ["required"] = new[] { "path" }
            },
            async args =>
            {
                if (!args.TryGetValue("path", out var pathObj) || pathObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };

                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path);
                Directory.CreateDirectory(fullPath);
                return new ToolResult { Content = $"Successfully created directory: {fullPath}" };
            });

        // delete_file: Delete a file
        executor.Register(
            "delete_file",
            "Delete a file from the filesystem.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the file to delete"
                    }
                },
                ["required"] = new[] { "path" }
            },
            async args =>
            {
                if (!args.TryGetValue("path", out var pathObj) || pathObj is not string path)
                    return new ToolResult { Content = "Error: missing required argument: path", IsError = true };

                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path);
                
                if (!File.Exists(fullPath))
                    return new ToolResult { Content = $"Error: file not found: {fullPath}", IsError = true };

                File.Delete(fullPath);
                return new ToolResult { Content = $"Successfully deleted file: {fullPath}" };
            });

        // search_files: Search for files by name pattern
        executor.Register(
            "search_files",
            "Search for files matching a pattern in a directory.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Directory to search in"
                    },
                    ["pattern"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Search pattern (e.g., '*.cs', 'test*')"
                    }
                },
                ["required"] = new[] { "pattern" }
            },
            async args =>
            {
                var pattern = "*";
                if (args.TryGetValue("pattern", out var patternObj) && patternObj is string p)
                    pattern = p;

                var searchPath = ".";
                if (args.TryGetValue("path", out var pathObj) && pathObj is string sp)
                    searchPath = sp;

                var fullPath = Path.IsPathRooted(searchPath) ? searchPath : Path.Combine(rootDir, searchPath);
                
                if (!Directory.Exists(fullPath))
                    return new ToolResult { Content = $"Error: directory not found: {fullPath}", IsError = true };

                var sb = new StringBuilder();
                var files = Directory.GetFiles(fullPath, pattern, SearchOption.AllDirectories);
                
                foreach (var file in files.Take(100))
                    sb.AppendLine(file);

                if (files.Length > 100)
                    sb.AppendLine($"\n... and {files.Length - 100} more files");

                var result = sb.ToString();
                if (result.Length > 10000)
                    result = result.Substring(0, 10000) + "\n... (truncated)";

                return new ToolResult { Content = result };
            });
    }
}
