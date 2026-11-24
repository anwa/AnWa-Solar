using System.Net;
using System.Text;

namespace AnWaSolar.Core;

/// <summary>
/// HTML page of the report preview and of the manual. Formulas that Markdig wrote as <c>\( … \)</c> and
/// <c>\[ … \]</c> are rendered by KaTeX, which is shipped with the application and served from <c>/katex/</c> of the
/// same local host. The page follows the light or dark colour scheme of the application.
/// </summary>
public static class HtmlDocument
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
