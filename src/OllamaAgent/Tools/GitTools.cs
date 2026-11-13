using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Git-related tools
/// </summary>
public static class GitTools
{
    public static void Register(ToolExecutor executor)
    {
        // git_status: Show working tree status
        executor.Register(
            "git_status",
            "Show the working tree status in a Git repository.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the Git repository (default: current directory)"
                    }
                }
            },
            async args => await ExecuteGitAsync(args, ".", new[] { "status", "--short" }));

        // git_diff: Show changes
        executor.Register(
            "git_diff",
            "Show changes between working directory and staged changes. If a file is specified, shows diff for that file only.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the Git repository (default: current directory)"
                    },
                    ["file"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional: specific file to show diff for"
                    }
                }
            },
            async args =>
            {
                var path = ".";
                if (args.TryGetValue("path", out var pathObj) && pathObj is string p)
                    path = p;

                if (args.TryGetValue("file", out var fileObj) && fileObj is string file)
                    return await ExecuteGitAsync(args, path, new[] { "diff", file });

                return await ExecuteGitAsync(args, path, new[] { "diff" });
            });

        // git_log: Show commit log
        executor.Register(
            "git_log",
            "Show the commit log for a Git repository.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the Git repository (default: current directory)"
                    },
                    ["max_commits"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum number of commits to show (default: 20)"
                    }
                }
            },
            async args =>
            {
                var path = ".";
                if (args.TryGetValue("path", out var pathObj) && pathObj is string p)
                    path = p;

                var maxCommits = 20;
                if (args.TryGetValue("max_commits", out var maxObj) && maxObj is int max)
                    maxCommits = max;

                return await ExecuteGitAsync(args, path, new[]
                {
                    "log",
                    "--pretty=format:%h|%s|%an|%ad",
                    "--date=short",
                    "-n",
                    maxCommits.ToString()
                });
            });

        // git_branch: List or manage branches
        executor.Register(
            "git_branch",
            "List branches in a Git repository, or create/switch branches.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the Git repository (default: current directory)"
                    },
                    ["action"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Action: 'list' (default), 'create', or 'switch'"
                    },
                    ["branch"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Branch name (for create/switch actions)"
                    }
                }
            },
            async args =>
            {
                var path = ".";
                if (args.TryGetValue("path", out var pathObj) && pathObj is string p)
                    path = p;

                if (!args.TryGetValue("action", out var actionObj) || actionObj is not string action)
                    return await ExecuteGitAsync(args, path, new[] { "branch", "-v" });

                if (!args.TryGetValue("branch", out var branchObj) || branchObj is not string branch)
                    return new ToolResult { Content = "Error: branch name required for create/switch action", IsError = true };

                var gitArgs = action switch
                {
                    "list" => new[] { "branch", "-v" },
                    "create" => new[] { "checkout", "-b", branch },
                    "switch" => new[] { "checkout", branch },
                    _ => null
                };

                if (gitArgs == null)
                    return new ToolResult { Content = $"Unknown action: {action}", IsError = true };

                return await ExecuteGitAsync(args, path, gitArgs);
            });

        // git_remote: Show or manage remotes
        executor.Register(
            "git_remote",
            "Show or manage Git remotes (remote repositories).",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the Git repository (default: current directory)"
                    },
                    ["action"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Action: 'list' (default), 'add'"
                    },
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Remote name (for add action)"
                    },
                    ["url"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Remote URL (for add action)"
                    }
                }
            },
            async args =>
            {
                var path = ".";
                if (args.TryGetValue("path", out var pathObj) && pathObj is string p)
                    path = p;

                if (!args.TryGetValue("action", out var actionObj) || actionObj is not string action)
                    return await ExecuteGitAsync(args, path, new[] { "remote", "-v" });

                var gitArgs = action switch
                {
                    "list" => new[] { "remote", "-v" },
                    "add" => null,
                    _ => null
                };

                if (gitArgs != null)
                    return await ExecuteGitAsync(args, path, gitArgs);

                if (action == "add")
                {
                    if (!args.TryGetValue("name", out var nameObj) || nameObj is not string name)
                        return new ToolResult { Content = "Error: remote name required", IsError = true };
                    if (!args.TryGetValue("url", out var urlObj) || urlObj is not string url)
                        return new ToolResult { Content = "Error: remote URL required", IsError = true };
                    return await ExecuteGitAsync(args, path, new[] { "remote", "add", name, url });
                }

                return new ToolResult { Content = $"Unknown action: {action}", IsError = true };
            });
    }

    private static async Task<ToolResult> ExecuteGitAsync(Dictionary<string, object> args, string path, string[] gitArgs)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path);
        
        if (!Directory.Exists(Path.Combine(fullPath, ".git")))
            return new ToolResult { Content = $"Error: not a Git repository: {fullPath}", IsError = true };

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{fullPath}\" {string.Join(" ", gitArgs)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process");
        await process.WaitForExitAsync();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        var output = stdout + stderr;
        
        // Format git log output
        if (gitArgs.Contains("--pretty=format:") && !string.IsNullOrEmpty(output))
        {
            var sb = new StringBuilder();
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|', 4);
                if (parts.Length == 4)
                    sb.AppendLine($"📝 {parts[0]} | {parts[1]} | {parts[2]} | {parts[3]}");
                else
                    sb.AppendLine(line);
            }
            output = sb.ToString();
        }

        if (output.Length > 10000)
            output = output.Substring(0, 10000) + "\n... (truncated)";

        return new ToolResult { Content = output };
    }
}
