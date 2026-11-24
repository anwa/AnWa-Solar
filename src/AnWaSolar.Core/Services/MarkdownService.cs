using Markdig;

using Microsoft.Extensions.Configuration;

namespace AnWaSolar.Core.Services;

public interface IMarkdownService
{
    string ToHtml(string markdown);
}

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService(IConfiguration cfg)
    {
        var advanced = bool.TryParse(cfg["AnWaSolar:Markdown:UseAdvancedExtensions"], out var useAdv) && useAdv;

        // Raw HTML is never passed through: names from the master data end up in the report.
        var builder = new MarkdownPipelineBuilder().DisableHtml();
        if (advanced)
        {
            // Includes the mathematics extension, which writes $…$ as \( … \) and $$…$$ as \[ … \] for KaTeX.
            builder.UseAdvancedExtensions();
        }

        _pipeline = builder.Build();
    }

    public string ToHtml(string markdown)
        => Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
}
