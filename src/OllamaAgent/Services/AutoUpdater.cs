using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaAgent.Services;

/// <summary>
/// GitHub release for auto-update
/// </summary>
public class GitHubRelease
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
}

public class GitHubAsset
{
    public string Name { get; set; } = string.Empty;
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
}

/// <summary>
/// Auto-update checker using GitHub releases
/// </summary>
public class AutoUpdater : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _githubRepo;
    private readonly string _currentVersion;
    private bool _disposed;

    public AutoUpdater(string githubRepo, string currentVersion)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _githubRepo = githubRepo;
        _currentVersion = currentVersion;
    }

    /// <summary>
    /// Check for updates. Returns null if no update available.
    /// </summary>
    public async Task<GitHubRelease?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_githubRepo}/releases/latest";
            var response = await _httpClient.GetAsync(url, ct);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            
            if (release == null) return null;

            // Compare versions
            if (IsNewerVersion(release.TagName, _currentVersion))
            {
                return release;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNewerVersion(string newVersion, string currentVersion)
    {
        // Strip 'v' prefix if present
        newVersion = newVersion.TrimStart('v');
        currentVersion = currentVersion.TrimStart('v');

        try
        {
            var newVer = new Version(newVersion);
            var currentVer = new Version(currentVersion);
            return newVer > currentVer;
        }
        catch
        {
            // Fallback: string comparison
            return string.Compare(newVersion, currentVersion, StringComparison.Ordinal) > 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
