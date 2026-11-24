using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace AnWaSolar.Core;

/// <summary>
/// HTML page of the report preview and of the manual. Formulas that Markdig wrote as <c>\( … \)</c> and
/// <c>\[ … \]</c> are rendered by KaTeX, which is shipped with the application and served from <c>/katex/</c> of the
/// same local host. The page follows the light or dark colour scheme of the application.
/// </summary>
public static partial class HtmlDocument
{
    private const string Style = """
        :root { color-scheme: light dark; --accent: #3F51B5; --muted-bg: #F1F3F9; --border: #D0D4E4; }
        @media (prefers-color-scheme: dark) { :root { --accent: #9FA8DA; --muted-bg: #2A2D3A; --border: #474B5C; } }
        body { font-family: 'Segoe UI', sans-serif; font-size: 14px; line-height: 1.5; margin: 16px 24px; }
        h1, h2, h3 { color: var(--accent); }
        a { color: var(--accent); }
        code { font-family: Consolas, monospace; background: var(--muted-bg); padding: 0 3px; border-radius: 3px; }
        pre { background: var(--muted-bg); padding: 8px 10px; border-radius: 4px; overflow-x: auto; }
        pre code { padding: 0; }
        table { border-collapse: collapse; margin: 8px 0; }
        th, td { border: 1px solid var(--border); padding: 4px 8px; text-align: left; vertical-align: top; }
        th { background: var(--muted-bg); }
        blockquote { margin: 8px 0; padding: 4px 12px; border-left: 4px solid var(--accent); background: var(--muted-bg); }
        """;

    /// <summary>Full HTML document for a converted Markdown body.</summary>
    public static string Create(string bodyHtml, string title)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"color-scheme\" content=\"light dark\">");
        html.AppendLine($"<title>{WebUtility.HtmlEncode(title)}</title>");
        html.AppendLine("<link rel=\"stylesheet\" href=\"/katex/katex.min.css\">");
        html.AppendLine("<style>");
        html.AppendLine(Style);
        html.AppendLine("</style>");
        html.AppendLine("<script defer src=\"/katex/katex.min.js\"></script>");
        html.AppendLine("<script defer src=\"/katex/auto-render.min.js\"></script>");
        html.AppendLine("<script>");
        html.AppendLine("document.addEventListener('DOMContentLoaded', function () {");
        html.AppendLine("  renderMathInElement(document.body, {");
        html.AppendLine("    delimiters: [ { left: '\\\\[', right: '\\\\]', display: true }, { left: '\\\\(', right: '\\\\)', display: false } ],");
        html.AppendLine("    throwOnError: false");
        html.AppendLine("  });");
        html.AppendLine("});");
        html.AppendLine("</script>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine(bodyHtml ?? string.Empty);
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    /// <summary>
    /// Turns a report page into a single file that works offline and without scripts: scripts are removed (formulas must
    /// already be rendered, for example the DOM of the preview), the KaTeX style sheet is inlined with its fonts as data
    /// URIs, and the page always uses the light colours.
    /// </summary>
    /// <param name="html">The page, as created by <see cref="Create"/> or as rendered by the preview.</param>
    /// <param name="katexDirectory">Folder with <c>katex.min.css</c> and <c>fonts</c>; missing files are left out.</param>
    public static string CreateStandalone(string html, string katexDirectory)
    {
        ArgumentNullException.ThrowIfNull(html);
        string result = ScriptPattern().Replace(html, string.Empty);
        result = KatexLinkPattern().Replace(result, _ => "<style>" + InlineKatexCss(katexDirectory) + "</style>");
        result = DarkSchemePattern().Replace(result, string.Empty);
        result = result
            .Replace("<meta name=\"color-scheme\" content=\"light dark\">", "<meta name=\"color-scheme\" content=\"light\">", StringComparison.Ordinal)
            .Replace("color-scheme: light dark;", "color-scheme: light;", StringComparison.Ordinal);
        return result.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ? result : "<!DOCTYPE html>\n" + result;
    }

    // The KaTeX style sheet with the woff2 fonts as data URIs; the woff and ttf alternatives are not shipped.
    private static string InlineKatexCss(string katexDirectory)
    {
        string cssPath = Path.Combine(katexDirectory, "katex.min.css");
        if (!File.Exists(cssPath))
        {
            return string.Empty;
        }

        string css = AlternativeFontPattern().Replace(File.ReadAllText(cssPath), string.Empty);
        return FontUrlPattern().Replace(css, match =>
        {
            string font = Path.Combine(katexDirectory, "fonts", match.Groups[1].Value);
            return File.Exists(font)
                ? $"url(data:font/woff2;base64,{Convert.ToBase64String(File.ReadAllBytes(font))})"
                : match.Value;
        });
    }

    [GeneratedRegex(@"<script\b[^>]*>.*?</script>\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptPattern();

    [GeneratedRegex(@"<link\b[^>]*href=""/katex/katex\.min\.css""[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KatexLinkPattern();

    [GeneratedRegex(@"@media \(prefers-color-scheme: dark\)[^\n]*\n?", RegexOptions.CultureInvariant)]
    private static partial Regex DarkSchemePattern();

    [GeneratedRegex(@",url\(fonts/[^)]+\.(?:woff|ttf)\) format\(""[^""]+""\)", RegexOptions.CultureInvariant)]
    private static partial Regex AlternativeFontPattern();

    [GeneratedRegex(@"url\(fonts/([A-Za-z0-9_\-]+\.woff2)\)", RegexOptions.CultureInvariant)]
    private static partial Regex FontUrlPattern();

    /// <summary>
    /// Resolves a request path such as <c>/katex/fonts/KaTeX_Main-Regular.woff2</c> to a file below
    /// <paramref name="katexDirectory"/>. Paths outside that folder and unknown file types are rejected.
    /// </summary>
    public static bool TryResolveAsset(string katexDirectory, string requestPath, out string filePath, out string contentType)
    {
        filePath = string.Empty;
        contentType = string.Empty;
        const string Prefix = "/katex/";
        if (string.IsNullOrEmpty(requestPath) || !requestPath.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string root = Path.GetFullPath(katexDirectory) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(requestPath[Prefix.Length..])));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        contentType = Path.GetExtension(candidate).ToUpperInvariant() switch
        {
            ".CSS" => "text/css",
            ".JS" => "text/javascript",
            ".WOFF2" => "font/woff2",
            _ => string.Empty,
        };
        if (contentType.Length == 0 || !File.Exists(candidate))
        {
            return false;
        }

        filePath = candidate;
        return true;
    }
}
