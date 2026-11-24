using System.Text.RegularExpressions;

using AnWaSolar.Core.Manual;

namespace AnWaSolar.Core.Tests;

public sealed partial class ManualTests
{
    private static ManualIndex Index(string? toc = null) => new(new Dictionary<string, string>
    {
        ["toc.json"] = toc ?? """
            {
              "title": "Test Manual",
              "start": "README.md",
              "pages": [
                { "path": "README.md", "title": "Start" },
                { "path": "calculation.md", "title": "Calculation" },
                { "path": "calculation/checks.md", "title": "Checks" },
                { "path": "report.md", "title": "Report" }
              ]
            }
            """,
        ["README.md"] = "# Start\n\nSee [checks](calculation/checks.md#max-dc-voltage) and [mail](mailto:someone@example.com).",
        ["calculation.md"] = "# Calculation\n\nTemperature correction with the **Voc** coefficient.",
        ["calculation/checks.md"] = "# Checks\n\n## Max. DC voltage\n\nThe open-circuit voltage at Tmin.\n\nBack to [report](../report.md).",
        ["report.md"] = "# Report\n\nCopy the Markdown.",
        ["notes.txt"] = "ignored",
    });

    [Fact]
    public void Tree_follows_toc_order_and_nests_sub_pages()
    {
        ManualIndex index = Index();

        Assert.Equal("Test Manual", index.Title);
        Assert.Equal("README.md", index.StartPage);
        Assert.Equal(["Start", "Calculation", "Report"], index.Tree.Select(n => n.Title));
        Assert.Equal("Checks", Assert.Single(index.Tree[1].Children).Title);
        Assert.Equal(4, index.Pages.Count);
    }

    [Fact]
    public void Invalid_toc_keeps_all_pages()
    {
        ManualIndex index = Index("{ not json");

        Assert.Equal("Manual", index.Title);
        Assert.Equal("README.md", index.StartPage);
        Assert.Equal("Checks", index.TitleOf("calculation/checks.md"));
    }

    [Fact]
    public void Links_resolve_relative_paths_anchors_and_external_targets()
    {
        ManualIndex index = Index();

        ManualLink checks = index.Resolve("README.md", "calculation/checks.md#max-dc-voltage");
        Assert.Equal("calculation/checks.md", checks.PagePath);
        Assert.Equal("max-dc-voltage", checks.Anchor);
        Assert.Equal("report.md", index.Resolve("calculation/checks.md", "../report.md").PagePath);
        Assert.Equal("mailto:someone@example.com", index.Resolve("README.md", "mailto:someone@example.com").ExternalUrl);
    }

    [Fact]
    public void Search_finds_titles_and_text()
    {
        ManualIndex index = Index();

        ManualHit hit = Assert.Single(index.Search("open-circuit"));
        Assert.Equal("calculation/checks.md", hit.Path);
        Assert.Equal("Test Manual › Calculation", hit.Breadcrumb);
        Assert.Equal("report.md", index.Search("Report")[0].Path);
        Assert.Empty(index.Search("x"));
    }

    [Fact]
    public void Shipped_manual_is_embedded_and_its_links_and_anchors_resolve()
    {
        ManualIndex index = new(ManualResources.Load(typeof(ManualTests).Assembly));

        Assert.False(index.IsEmpty);
        Assert.Equal("README.md", index.StartPage);
        foreach (string page in index.Pages)
        {
            foreach (Match link in MarkdownLink().Matches(index.Text(page)!))
            {
                ManualLink target = index.Resolve(page, link.Groups[1].Value);
                if (target.ExternalUrl is not null)
                {
                    continue;
                }

                string targetPage = target.PagePath ?? page;
                Assert.True(index.Text(targetPage) is not null, $"{page}: link {link.Value} points to a missing page.");
                if (target.Anchor is { } anchor)
                {
                    IEnumerable<string> anchors = index.Text(targetPage)!.Split('\n')
                        .Where(l => l.StartsWith('#'))
                        .Select(l => ManualIndex.Slug(l.TrimStart('#')));
                    Assert.True(anchors.Contains(anchor), $"{page}: anchor #{anchor} not found in {targetPage}.");
                }
            }
        }
    }

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex MarkdownLink();
}
