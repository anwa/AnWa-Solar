using Markdig;
using Microsoft.Extensions.Configuration;

namespace AnWaSolar;

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

        var builder = new MarkdownPipelineBuilder();
        if (advanced)
        {
            builder.UseAdvancedExtensions();
        }
        _pipeline = builder.Build();
    }

    public string ToHtml(string markdown)
        => Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
}
