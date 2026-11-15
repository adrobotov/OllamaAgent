using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OllamaAgent.Helpers;

/// <summary>
/// Pre-processes LaTeX in markdown to convert unsupported CSharpMath commands
/// into supported equivalents. CSharpMath does not support \boxed, \tag, \color,
/// \begin{cases}, \begin{aligned}, etc.
/// </summary>
public static class LatexPreprocessor
{
    /// <summary>
    /// Function that takes the content inside braces and returns replacement text.
    /// </summary>
    public delegate string BracedContentHandler(string content);

    public static string Preprocess(string latex)
    {
        if (string.IsNullOrEmpty(latex))
            return latex;

        var result = latex;

        // \boxed{...} → \displaystyle{...} (CSharpMath doesn't support \fbox)
        result = ReplaceBracedCommand(result, @"\boxed", content =>
            string.Concat(@"\displaystyle{", content, "}"));

        // \mathbf{...} → \textbf{...}
        result = ReplaceBracedCommand(result, @"\mathbf", content =>
            string.Concat(@"\textbf{", content, "}"));

        // \mathbb{...} → \mathbf{...} (blackboard bold not supported)
        result = ReplaceBracedCommand(result, @"\mathbb", content =>
            string.Concat(@"\mathbf{", content, "}"));

        // \mathsf{...} → \text{...}
        result = ReplaceBracedCommand(result, @"\mathsf", content =>
            string.Concat(@"\text{", content, "}"));

        // \mathrm{...} → \text{...}
        result = ReplaceBracedCommand(result, @"\mathrm", content =>
            string.Concat(@"\text{", content, "}"));

        // \underline{...} → \overline{...} (underline not supported)
        result = ReplaceBracedCommand(result, @"\underline", content =>
            string.Concat(@"\overline{", content, "}"));

        // \not{...} → \neq{...}
        result = ReplaceBracedCommand(result, @"\not", content =>
            string.Concat(@"\neq{", content, "}"));

        // \textcolor{...}{...} → strip color, keep content
        result = Regex.Replace(result, @"\\textcolor\{[^}]*\}\{([^}]*)\}", "$1");

        // \color{...}{...} → strip color, keep content
        result = Regex.Replace(result, @"\\color\{[^}]*\}\{([^}]*)\}", "$1");

        // \ensuremath{...} → unwrap
        result = Regex.Replace(result, @"\\ensuremath\{([^}]*)\}", "$1");

        // Fix unmatched \left and \right
        result = FixUnmatchedDelimiters(result);

        // Handle \begin{...}...\end{...} environments
        result = HandleEnvironments(result);

        // Remove unsupported commands
        result = RemoveUnsupportedCommands(result);

        return result;
    }

    /// <summary>
    /// Replace a LaTeX command that takes a single braced argument with arbitrary nesting.
    /// E.g., \boxed{\frac{a}{b}} → replacement(\frac{a}{b})
    /// </summary>
    private static string ReplaceBracedCommand(string input, string commandName,
        BracedContentHandler handler)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(commandName))
            return input;

        var result = new List<string>();
        int i = 0;
        int lastEnd = 0;

        while (i < input.Length)
        {
            // Check if command matches at position i
            if (input.Substring(i).StartsWith(commandName, StringComparison.Ordinal))
            {
                int cmdStart = i;
                int cmdEnd = i + commandName.Length;

                // Skip to find opening brace, skipping whitespace
                int j = cmdEnd;
                while (j < input.Length && char.IsWhiteSpace(input[j]))
                    j++;

                if (j < input.Length && input[j] == '{')
                {
                    // Find matching closing brace with proper nesting
                    int braceStart = j;
                    int depth = 0;
                    int k = j;
                    while (k < input.Length)
                    {
                        if (input[k] == '\\' && k + 1 < input.Length)
                        {
                            k += 2; // Skip escaped character
                            continue;
                        }

                        if (input[k] == '{')
                            depth++;
                        else if (input[k] == '}')
                        {
                            depth--;
                            if (depth == 0)
                                break;
                        }
                        k++;
                    }

                    if (depth == 0)
                    {
                        // Found matching brace
                        int braceEnd = k;
                        string content = input.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        string replacement = handler(content);

                        result.Add(input.Substring(lastEnd, cmdStart - lastEnd));
                        result.Add(replacement);
                        lastEnd = braceEnd + 1;
                        i = braceEnd + 1;
                        continue;
                    }
                }

                // No matching brace found, skip past command
                i = cmdEnd;
            }
            else
            {
                i++;
            }
        }

        if (lastEnd == 0)
            return input;

        result.Add(input.Substring(lastEnd));
        return string.Concat(result);
    }

    /// <summary>
    /// Fix unmatched \left and \right delimiters.
    /// </summary>
    private static string FixUnmatchedDelimiters(string input)
    {
        var leftCount = Regex.Matches(input, @"\\left[.\[\]()|]").Count;
        var rightCount = Regex.Matches(input, @"\\right[.\[\]()|]").Count;

        if (leftCount > rightCount)
        {
            input += new string('.', leftCount - rightCount);
        }
        else if (rightCount > leftCount)
        {
            var excess = rightCount - leftCount;
            var matches = Regex.Matches(input, @"\\right[.\[\]()|]");
            foreach (Match m in matches.Take(excess).Reverse())
            {
                input = input.Remove(m.Index, m.Length);
            }
        }

        return input;
    }

    /// <summary>
    /// Handle \begin{...}...\end{...} environments.
    /// </summary>
    private static string HandleEnvironments(string input)
    {
        // \begin{cases} → \begin{aligned} with & alignment
        input = Regex.Replace(input,
            @"\\begin\{cases\}((?:[^\\]|\\[^a-zA-Z]|\\[a-zA-Z]+[^\\{]*)*?)\\end\{cases\}",
            m => ConvertCasesToAligned(m.Groups[1].Value),
            RegexOptions.Singleline);

        // \begin{aligned} → flatten to manual alignment
        input = Regex.Replace(input,
            @"\\begin\{aligned\}((?:[^\\]|\\[^a-zA-Z]|\\[a-zA-Z]+[^\\{]*)*?)\\end\{aligned\}",
            m => ConvertAlignedToManual(m.Groups[1].Value),
            RegexOptions.Singleline);

        // \begin{gathered} → unwrap
        input = Regex.Replace(input,
            @"\\begin\{gathered\}((?:[^\\]|\\[^a-zA-Z]|\\[a-zA-Z]+[^\\{]*)*?)\\end\{gathered\}",
            m => m.Groups[1].Value.Trim(),
            RegexOptions.Singleline);

        // \begin{split} → unwrap
        input = Regex.Replace(input,
            @"\\begin\{split\}((?:[^\\]|\\[^a-zA-Z]|\\[a-zA-Z]+[^\\{]*)*?)\\end\{split\}",
            m => m.Groups[1].Value.Trim(),
            RegexOptions.Singleline);

        return input;
    }

    /// <summary>
    /// Convert \begin{cases} content \end{cases} to \begin{aligned} format.
    /// </summary>
    private static string ConvertCasesToAligned(string content)
    {
        var lines = Regex.Split(content, @"\\(?<!\\)\\");
        var result = "\\begin{aligned}";
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var parts = trimmed.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    result += part;
                    if (i < parts.Length - 1)
                        result += "\\quad";
                }
            }
            result += "\\\\";
        }
        result += "\\end{aligned}";
        return result;
    }

    /// <summary>
    /// Convert \begin{aligned} content \end{aligned} to manual alignment.
    /// </summary>
    private static string ConvertAlignedToManual(string content)
    {
        var lines = Regex.Split(content, @"\\(?<!\\)\\");
        var result = "";
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var parts = trimmed.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    result += part;
                    if (i < parts.Length - 1)
                        result += "\\quad";
                }
            }
            if (!string.IsNullOrEmpty(result))
                result += "\\\\";
        }
        return result.TrimEnd('\\');
    }

    /// <summary>
    /// Remove or neutralize unsupported LaTeX commands.
    /// </summary>
    private static string RemoveUnsupportedCommands(string input)
    {
        // \hspace{...}, \vspace{...} → remove
        input = Regex.Replace(input, @"\\hspace\{[^}]*\}", "");
        input = Regex.Replace(input, @"\\vspace\{[^}]*\}", "");

        // \rule{...}{...} → remove
        input = Regex.Replace(input, @"\\rule\{[^}]*\}\{[^}]*\}", "");

        // \item → remove
        input = Regex.Replace(input, @"\\item\s*", "");

        // \label{...}, \ref{...}, \cite{...} → remove
        input = Regex.Replace(input, @"\\label\{[^}]*\}", "");
        input = Regex.Replace(input, @"\\ref\{[^}]*\}", "");
        input = Regex.Replace(input, @"\\cite\{[^}]*\}", "");

        // \section, \subsection, etc. → remove
        input = Regex.Replace(input, @"\\(sub)?(section|chapter|paragraph)\*?\{[^}]*\}", "");

        // \big, \Big, \bigg, \Bigg → remove (size modifiers)
        input = Regex.Replace(input, @"\\(big|Big|bigg|Bigg)l?", "");
        input = Regex.Replace(input, @"\\(big|Big|bigg|Bigg)r?", "");

        // \limits, \nolimits → remove
        input = Regex.Replace(input, @"\\(no)?limits", "");

        // \allowbreak, \break → remove
        input = Regex.Replace(input, @"\\allowbreak", "");
        input = Regex.Replace(input, @"\\break", "");

        // \colon → :
        input = Regex.Replace(input, @"\\colon", ":");

        // \; \, \: → space
        input = Regex.Replace(input, @"\\[;,]", " ");
        input = Regex.Replace(input, @"\\;", " ");

        return input;
    }
}
