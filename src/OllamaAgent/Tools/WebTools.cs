using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OllamaAgent.Tools;

namespace OllamaAgent.Tools;

/// <summary>
/// Web-related tools
/// </summary>
public class WebClient
{
    private readonly HttpClient _httpClient;

    public WebClient()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OllamaAgent", "1.0"));
    }

    public async Task<string> FetchUrlAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var text = HtmlToText(body);

        if (text.Length > 20000)
            text = text.Substring(0, 20000) + "\n... (truncated, content too large)";

        return text;
    }

    public async Task<string> SearchWebAsync(string query)
    {
        var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        var response = await _httpClient.GetAsync(searchUrl);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var results = ParseDuckDuckGoResults(html);

        if (results.Count == 0)
            return $"No results found for: {query}";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Search results for: {query}\n");
        
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"{i + 1}. {r.Title}");
            sb.AppendLine($"   {r.Snippet}");
            sb.AppendLine($"   {r.URL}\n");
        }

        return sb.ToString();
    }

    private static List<SearchResult> ParseDuckDuckGoResults(string html)
    {
        var results = new List<SearchResult>();
        var lines = html.Split('\n');
        
        string? currentTitle = null;
        string? currentSnippet = null;
        string? currentUrl = null;

        foreach (var line in lines)
        {
            // Extract title
            if (line.Contains("class=\"result__a\""))
            {
                var start = line.IndexOf('>');
                var end = line.LastIndexOf('<');
                if (start != -1 && end != -1 && end > start)
                    currentTitle = line.Substring(start + 1, end - start - 1).Trim();
            }
            
            // Extract URL
            if (line.Contains("data-ved=\""))
            {
                var start = line.IndexOf("href=\"");
                if (start != -1)
                {
                    start += 6;
                    var end = line.IndexOf('"', start);
                    if (end != -1)
                    {
                        currentUrl = line.Substring(start, end - start).Replace("&amp;", "&");
                    }
                }
            }
            
            // Extract snippet
            if (line.Contains("class=\"result__snippet\""))
            {
                var start = line.IndexOf('>');
                var end = line.LastIndexOf('<');
                if (start != -1 && end != -1 && end > start)
                    currentSnippet = line.Substring(start + 1, end - start - 1).Trim();
            }

            // When we have all parts, add the result
            if (currentTitle != null && currentUrl != null && currentSnippet != null)
            {
                results.Add(new SearchResult { Title = currentTitle, URL = currentUrl, Snippet = currentSnippet });
                currentTitle = currentSnippet = currentUrl = null;
            }
        }

        return results;
    }

    private static string HtmlToText(string html)
    {
        // Remove script and style tags
        html = Regex.Replace(html, "<script[^>]*>.*?</script>", "", RegexOptions.Singleline);
        html = Regex.Replace(html, "<style[^>]*>.*?</style>", "", RegexOptions.Singleline);

        // Remove HTML tags
        html = Regex.Replace(html, "<[^>]+>", " ");

        // Collapse whitespace
        html = Regex.Replace(html, @"\s+", " ");

        return html.Trim();
    }
}

/// <summary>
/// Search result from web search
/// </summary>
public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

/// <summary>
/// Web tools registration
/// </summary>
public static class WebTools
{
    public static void Register(ToolExecutor executor, WebClient client)
    {
        // load_url: Fetch URL content
        executor.Register(
            "load_url",
            "Fetch and read content from a URL. Returns the page content as plain text (HTML stripped).",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["url"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The URL to fetch content from"
                    }
                },
                ["required"] = new[] { "url" }
            },
            async args =>
            {
                if (!args.TryGetValue("url", out var urlObj) || urlObj is not string url)
                    return new ToolResult { Content = "Error: missing required argument: url", IsError = true };

                var content = await client.FetchUrlAsync(url);
                return new ToolResult { Content = content };
            });

        // search_web: Search the web
        executor.Register(
            "search_web",
            "Search the web for information using DuckDuckGo. Returns search results with titles, snippets, and URLs.",
            new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The search query"
                    }
                },
                ["required"] = new[] { "query" }
            },
            async args =>
            {
                if (!args.TryGetValue("query", out var queryObj) || queryObj is not string query)
                    return new ToolResult { Content = "Error: missing required argument: query", IsError = true };

                var results = await client.SearchWebAsync(query);
                return new ToolResult { Content = results };
            });
    }
}
