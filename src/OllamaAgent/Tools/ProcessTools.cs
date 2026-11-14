using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Process management tools
/// </summary>
public static class ProcessTools
{
    public static void Register(ToolExecutor executor)
    {
        // run_command: Execute a shell command (sandboxed)
        executor.Register(
            "run_command",
            "Execute a shell command and return the stdout output. Use for running any system command. Note: Some dangerous commands are blocked for security.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["command"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The shell command to execute"
                    }
                },
                ["required"] = new[] { "command" }
            },
            async args =>
            {
                if (!args.TryGetValue("command", out var cmdObj) || cmdObj is not string command)
                    return new ToolResult { Content = "Error: missing required argument: command", IsError = true };

                // Security: Check for dangerous commands
                var blockedPatterns = new[] { "rm -rf", "del /f /q", "format", "diskpart", "shutdown /s", "shutdown /r", "poweroff", "reboot", "mkfs", "dd if=", "chmod 777", "chown -R", "wget http", "curl http" };
                var commandLower = command.ToLower();
                foreach (var pattern in blockedPatterns)
                {
                    if (commandLower.Contains(pattern))
                    {
                        return new ToolResult { Content = $"Error: command blocked for security reasons: '{pattern}'", IsError = true };
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "sh",
                    Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {command}" : $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process");
                
                await process.WaitForExitAsync();
                
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();

                var output = stdout;
                if (!string.IsNullOrEmpty(stderr))
                    output += $"\nSTDERR:\n{stderr}";

                if (output.Length > 10000)
                    output = output.Substring(0, 10000) + "\n... (truncated)";

                return new ToolResult { Content = output };
            });

        // list_processes: List running processes
        executor.Register(
            "list_processes",
            "List all running processes on the system.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            async _ =>
            {
                var processes = Process.GetProcesses();
                var sb = new StringBuilder();
                sb.AppendLine($"Total processes: {processes.Length}\n");
                sb.AppendLine($"{new string(' ', 0) + "PID",-10} {new string(' ', 0) + "Name",-30} {new string(' ', 0) + "CPU (s)",-12} {new string(' ', 0) + "Memory (MB)",-12}");
                sb.AppendLine(new string('-', 70));

                foreach (var proc in processes.Take(200))
                {
                    try
                    {
                        var cpu = proc.TotalProcessorTime.TotalSeconds;
                        var memory = proc.WorkingSet64 / (1024 * 1024);
                        sb.AppendLine($"{proc.Id,-10} {proc.ProcessName,-30} {cpu,-12:F2} {memory,-12:F1}");
                    }
                    catch
                    {
                        // Access denied for some processes
                    }
                }

                var result = sb.ToString();
                if (result.Length > 10000)
                    result = result.Substring(0, 10000) + "\n... (truncated)";

                return new ToolResult { Content = result };
            });

        // kill_process: Terminate a process
        executor.Register(
            "kill_process",
            "Terminate a running process by its PID (process ID) or name.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["pid"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Process ID to terminate"
                    },
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Process name to terminate (alternative to PID)"
                    }
                },
                ["required"] = new[] { "pid" }
            },
            async args =>
            {
                if (args.TryGetValue("pid", out var pidObj) && pidObj is int pid)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        process.Kill(true);
                        await process.WaitForExitAsync();
                        return new ToolResult { Content = $"Successfully terminated process {pid}" };
                    }
                    catch (Exception ex)
                    {
                        return new ToolResult { Content = $"Error killing process {pid}: {ex.Message}", IsError = true };
                    }
                }

                if (args.TryGetValue("name", out var nameObj) && nameObj is string name)
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length == 0)
                        return new ToolResult { Content = $"No processes found with name: {name}", IsError = true };

                    var sb = new StringBuilder();
                    foreach (var proc in processes)
                    {
                        try
                        {
                            proc.Kill(true);
                            await proc.WaitForExitAsync();
                            sb.AppendLine($"Terminated process {proc.Id}");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"Error killing {proc.Id}: {ex.Message}");
                        }
                    }

                    return new ToolResult { Content = sb.ToString() };
                }

                return new ToolResult { Content = "Error: must provide either pid or name", IsError = true };
            });
    }
}
