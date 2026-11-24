using System.Text;

namespace AnWaSolar.Core.Calculation;

/// <summary>
/// HTML page of the report preview. Formulas that Markdig wrote as <c>\( … \)</c> and <c>\[ … \]</c> are rendered by
/// KaTeX, which is shipped with the application and served from the <c>katex/</c> folder next to the page.
/// </summary>
public static class ReportHtmlDocument
{
    /// <summary>Full HTML document for the converted Markdown body.</summary>
    public static string Create(string bodyHtml)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"color-scheme\" content=\"light dark\">");
        html.AppendLine("<title>Report</title>");
        html.AppendLine("<link rel=\"stylesheet\" href=\"katex/katex.min.css\">");
        html.AppendLine("<style>body { font-family: 'Segoe UI', sans-serif; font-size: 14px; line-height: 1.5; margin: 16px 24px; }</style>");
        html.AppendLine("<script defer src=\"katex/katex.min.js\"></script>");
        html.AppendLine("<script defer src=\"katex/auto-render.min.js\"></script>");
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
