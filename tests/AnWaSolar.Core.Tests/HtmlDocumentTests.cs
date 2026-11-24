

namespace AnWaSolar.Core.Tests;

public sealed class HtmlDocumentTests : IDisposable
{
    private readonly string _katex = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"), "katex");

    public HtmlDocumentTests()
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
        string html = HtmlDocument.Create("<p>body</p>", "Report & <test>");

        Assert.Contains("<p>body</p>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/katex/katex.min.css\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"/katex/katex.min.js\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"/katex/auto-render.min.js\"", html, StringComparison.Ordinal);
        Assert.Contains("<title>Report &amp; &lt;test&gt;</title>", html, StringComparison.Ordinal);
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
        Assert.True(HtmlDocument.TryResolveAsset(_katex, requestPath, out string file, out string type));
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
        Assert.False(HtmlDocument.TryResolveAsset(_katex, requestPath, out _, out _));
    }

    [Fact]
    public void Standalone_file_has_no_scripts_and_no_external_references()
    {
        File.WriteAllText(Path.Combine(_katex, "katex.min.css"),
            "@font-face{font-family:KaTeX_Main;src:url(fonts/KaTeX_Main-Regular.woff2) format(\"woff2\"),url(fonts/KaTeX_Main-Regular.woff) format(\"woff\"),url(fonts/KaTeX_Main-Regular.ttf) format(\"truetype\")}.katex{font:normal 1.21em KaTeX_Main}");
        string rendered = HtmlDocument.Create("<p>U = <span class=\"katex\">x</span></p>", "Report & <test>");

        string html = HtmlDocument.CreateStandalone(rendered, _katex);

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/katex/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("url(fonts/", html, StringComparison.Ordinal);
        Assert.Contains("url(data:font/woff2;base64,Zm9udA==)", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"katex\">x</span>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Report &amp; &lt;test&gt;</title>", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Standalone_file_uses_the_light_colours()
    {
        string html = HtmlDocument.CreateStandalone(HtmlDocument.Create("<p>x</p>", "Report"), _katex);

        Assert.DoesNotContain("prefers-color-scheme: dark", html, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"color-scheme\" content=\"light\">", html, StringComparison.Ordinal);
        Assert.Contains("color-scheme: light;", html, StringComparison.Ordinal);
    }
}
