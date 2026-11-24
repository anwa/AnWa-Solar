using AnWaSolar.Core.Calculation;

namespace AnWaSolar.Core.Tests;

public sealed class ReportHtmlDocumentTests : IDisposable
{
    private readonly string _katex = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"), "katex");

    public ReportHtmlDocumentTests()
    {
        Directory.CreateDirectory(Path.Combine(_katex, "fonts"));
        File.WriteAllText(Path.Combine(_katex, "katex.min.css"), "/* css */");
        File.WriteAllText(Path.Combine(_katex, "katex.min.js"), "// js");
        File.WriteAllText(Path.Combine(_katex, "fonts", "KaTeX_Main-Regular.woff2"), "font");
        File.WriteAllText(Path.Combine(_katex, "LICENSE"), "MIT");
        File.WriteAllText(Path.Combine(_katex, "..", "secret.css"), "outside");
    }

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_katex)!, recursive: true);

    [Fact]
    public void Document_loads_katex_and_renders_both_delimiters()
    {
        string html = ReportHtmlDocument.Create("<p>body</p>");

        Assert.Contains("<p>body</p>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"katex/katex.min.css\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"katex/katex.min.js\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"katex/auto-render.min.js\"", html, StringComparison.Ordinal);
        Assert.Contains("left: '\\\\[', right: '\\\\]', display: true", html, StringComparison.Ordinal);
        Assert.Contains("left: '\\\\(', right: '\\\\)', display: false", html, StringComparison.Ordinal);
        Assert.Contains("content=\"light dark\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/katex/katex.min.css", "text/css")]
    [InlineData("/katex/katex.min.js", "text/javascript")]
    [InlineData("/katex/fonts/KaTeX_Main-Regular.woff2", "font/woff2")]
    public void Katex_files_are_resolved(string requestPath, string expectedType)
    {
        Assert.True(ReportHtmlDocument.TryResolveAsset(_katex, requestPath, out string file, out string type));
        Assert.Equal(expectedType, type);
        Assert.StartsWith(Path.GetFullPath(_katex), file, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/katex/../secret.css")]
    [InlineData("/katex/%2E%2E/secret.css")]
    [InlineData("/katex/LICENSE")]
    [InlineData("/katex/missing.css")]
    [InlineData("/other/katex.min.css")]
    [InlineData("")]
    public void Other_paths_are_rejected(string requestPath)
    {
        Assert.False(ReportHtmlDocument.TryResolveAsset(_katex, requestPath, out _, out _));
    }
}
