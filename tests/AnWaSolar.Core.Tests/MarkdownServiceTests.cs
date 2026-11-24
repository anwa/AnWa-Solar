using AnWaSolar.Core.Services;

using Microsoft.Extensions.Configuration;

namespace AnWaSolar.Core.Tests;

public sealed class MarkdownServiceTests
{
    private static MarkdownService Create(bool advanced)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AnWaSolar:Markdown:UseAdvancedExtensions"] = advanced ? "true" : "false",
            })
            .Build();
        return new MarkdownService(configuration);
    }

    [Fact]
    public void Converts_headings_and_lists()
    {
        string html = Create(advanced: false).ToHtml("# Report\n\n- item");

        Assert.Contains("<h1>Report</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<li>item</li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Advanced_extensions_render_tables()
    {
        const string Table = "| a | b |\n|---|---|\n| 1 | 2 |";

        Assert.Contains("<table>", Create(advanced: true).ToHtml(Table), StringComparison.Ordinal);
        Assert.DoesNotContain("<table>", Create(advanced: false).ToHtml(Table), StringComparison.Ordinal);
    }

    [Fact]
    public void Null_input_returns_empty_html()
    {
        Assert.Equal(string.Empty, Create(advanced: false).ToHtml(null!));
    }
}
