using System;

namespace OllamaAgent.Helpers;

/// <summary>
/// Fuzzy search helper for text matching
/// </summary>
public static class FuzzySearch
{
    /// <summary>
    /// Simple case-insensitive substring match with fuzzy fallback
    /// Returns true if query is found as a substring or as a subsequence
    /// </summary>
    public static bool Contains(string text, string query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return string.IsNullOrEmpty(query);

        // Fast path: case-insensitive substring
        if (text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Fuzzy path: check if query chars appear in order
        return IsSubsequence(text, query);
    }

    private static bool IsSubsequence(string text, string query)
    {
        var textIndex = 0;
        var queryIndex = 0;
        var textLen = text.Length;
        var queryLen = query.Length;

        while (textIndex < textLen && queryIndex < queryLen)
        {
            if (char.ToLowerInvariant(text[textIndex]) == char.ToLowerInvariant(query[queryIndex]))
            {
                queryIndex++;
            }
            textIndex++;
        }

        return queryIndex == queryLen;
    }
}
