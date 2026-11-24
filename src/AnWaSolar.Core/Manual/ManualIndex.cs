using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnWaSolar.Core.Manual;

/// <summary>A node of the manual tree: a page (with <see cref="Path"/>) or a folder without page.</summary>
public sealed record ManualNode(string Title, string? Path, IReadOnlyList<ManualNode> Children);

/// <summary>A search hit with up to three matching lines.</summary>
public sealed record ManualHit(string Path, string Title, string Breadcrumb, bool TitleMatches, int MatchCount, IReadOnlyList<string> Snippets);

/// <summary>Target of a link inside a page.</summary>
/// <param name="PagePath">Manual page to open, or null.</param>
/// <param name="Anchor">Heading anchor on that page (or on the current page), or null.</param>
/// <param name="ExternalUrl">Web or mail link to open outside the application, or null.</param>
public sealed record ManualLink(string? PagePath, string? Anchor, string? ExternalUrl);

/// <summary>
/// The pages of the manual (Markdown files plus <c>toc.json</c>) with tree, titles, link
/// resolution and search. Paths are relative with <c>/</c>, for example <c>calculation/checks.md</c>.
/// </summary>
public sealed partial class ManualIndex
{
    private readonly Dictionary<string, string> _pages;
    private readonly List<string> _order = [];
    private readonly Dictionary<string, string> _titles = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="files">File contents by relative path; <c>toc.json</c> is optional.</param>
    public ManualIndex(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _pages = files.Where(f => f.Key.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(f => Normalize(f.Key), f => f.Value, StringComparer.OrdinalIgnoreCase);
        var toc = files.FirstOrDefault(f => string.Equals(Normalize(f.Key), "toc.json", StringComparison.OrdinalIgnoreCase)).Value;
        ReadToc(toc);
        Tree = BuildTree();
    }

    /// <summary>Manual title from <c>toc.json</c>.</summary>
    public string Title { get; private set; } = "Manual";

    /// <summary>Start page: <c>toc.start</c>, else <c>README.md</c>, else the first page.</summary>
    public string? StartPage { get; private set; }

    public bool IsEmpty => _pages.Count == 0;

    public IReadOnlyCollection<string> Pages => _pages.Keys;

    public IReadOnlyList<ManualNode> Tree { get; }

    public string? Text(string path) => _pages.GetValueOrDefault(Normalize(path));

    public static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/', '.');

    /// <summary>Title from <c>toc.json</c>, otherwise from the file name (<c>master-data.md</c> → "Master data").</summary>
    public string TitleOf(string path)
    {
        path = Normalize(path);
        if (_titles.TryGetValue(path, out var title))
        {
            return title;
        }

        var name = System.IO.Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Replace('-', ' ').Trim();
        name = Regex.Replace(name, @"^\d+\s*", string.Empty);
        return name.Length == 0 ? path : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>Titles of the parent pages or folders, for example "Manual › Calculation".</summary>
    public string Breadcrumb(string path)
    {
        var parts = new List<string> { Title };
        var folder = "";
        foreach (var segment in DirectoryOf(path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            folder = folder.Length == 0 ? segment : folder + "/" + segment;
            parts.Add(TitleOf(_pages.ContainsKey(folder + ".md") ? folder + ".md" : folder));
        }

        return string.Join(" › ", parts);
    }

    /// <summary>Resolves a link of <paramref name="currentPath"/>: relative page links (also <c>..</c>), anchors and external links.</summary>
    public ManualLink Resolve(string currentPath, string? href)
    {
        href = href?.Trim() ?? string.Empty;
        if (href.Length == 0)
        {
            return new ManualLink(null, null, null);
        }

        if (href.Contains("://", StringComparison.Ordinal) || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return new ManualLink(null, null, href);
        }

        var hash = href.IndexOf('#', StringComparison.Ordinal);
        var anchor = hash >= 0 ? href[(hash + 1)..] : null;
        var target = Uri.UnescapeDataString(hash >= 0 ? href[..hash] : href).Replace('\\', '/');
        if (target.Length == 0)
        {
            return new ManualLink(null, string.IsNullOrEmpty(anchor) ? null : anchor, null);
        }

        var parts = DirectoryOf(currentPath).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var segment in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (segment != ".")
            {
                parts.Add(segment);
            }
        }

        var combined = string.Join('/', parts);
        var page = _pages.Keys.FirstOrDefault(k => string.Equals(k, combined, StringComparison.OrdinalIgnoreCase));
        return new ManualLink(page, string.IsNullOrEmpty(anchor) ? null : anchor, null);
    }

    /// <summary>Pages whose title or text contains <paramref name="query"/> (at least two characters); title hits first.</summary>
    public IReadOnlyList<ManualHit> Search(string? query)
    {
        var q = query?.Trim() ?? string.Empty;
        if (q.Length < 2)
        {
            return [];
        }

        var hits = new List<ManualHit>();
        foreach (var (path, text) in _pages)
        {
            var title = TitleOf(path);
            var titleMatches = title.Contains(q, StringComparison.OrdinalIgnoreCase);
            var lines = text.Split('\n').Select(PlainText).Where(l => l.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            if (titleMatches || lines.Count > 0)
            {
                hits.Add(new ManualHit(path, title, Breadcrumb(path), titleMatches, lines.Count, [.. lines.Take(3).Select(l => Excerpt(l, q))]));
            }
        }

        return [.. hits.OrderByDescending(h => h.TitleMatches).ThenByDescending(h => h.MatchCount).ThenBy(h => SortIndex(h.Path))];
    }

    /// <summary>Anchor of a heading as on GitHub: lower case, spaces to <c>-</c>, punctuation and emoji removed.</summary>
    public static string Slug(string heading)
    {
        var text = new StringBuilder();
        foreach (var c in heading.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                text.Append(c);
            }
            else if (c == ' ')
            {
                text.Append('-');
            }
        }

        return text.ToString().Trim('-');
    }

    /// <summary>Text of a Markdown line without markup, for search and snippets.</summary>
    public static string PlainText(string line)
    {
        var s = line.Trim().TrimStart('#', '>', '-', '*', '|', ' ');
        s = MarkdownLink().Replace(s, "$1");
        return s.Replace("**", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("|", " ", StringComparison.Ordinal).Trim();
    }

    private static string Excerpt(string line, string q)
    {
        const int Max = 160;
        if (line.Length <= Max)
        {
            return line;
        }

        var at = line.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, at - 50);
        var length = Math.Min(Max, line.Length - start);
        return (start > 0 ? "… " : string.Empty) + line.Substring(start, length) + (start + length < line.Length ? " …" : string.Empty);
    }

    private static string DirectoryOf(string path)
    {
        var p = Normalize(path);
        var slash = p.LastIndexOf('/');
        return slash < 0 ? string.Empty : p[..slash];
    }

    private int SortIndex(string path)
    {
        var i = _order.FindIndex(o => string.Equals(o, path, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? int.MaxValue : i;
    }

    private void ReadToc(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    Title = title.GetString()!;
                }

                if (root.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
                {
                    StartPage = _pages.Keys.FirstOrDefault(k => string.Equals(k, Normalize(start.GetString()!), StringComparison.OrdinalIgnoreCase));
                }

                if (root.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
                {
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (page.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String)
                        {
                            var p = Normalize(path.GetString()!);
                            _order.Add(p);
                            if (page.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString()))
                            {
                                _titles[p] = t.GetString()!;
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // An invalid toc only loses order and titles; the pages are still shown.
            }
        }

        StartPage ??= _pages.Keys.FirstOrDefault(k => string.Equals(k, "README.md", StringComparison.OrdinalIgnoreCase))
            ?? _pages.Keys.OrderBy(SortIndex).ThenBy(k => k, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    private sealed class Builder(string title, string? path, int index)
    {
        public string Title { get; } = title;
        public string? Path { get; } = path;
        public int Index { get; } = index;
        public List<Builder> Children { get; } = [];

        public ManualNode Build() => new(Title, Path, [.. Children.OrderBy(c => c.Index).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase).Select(c => c.Build())]);
    }

    private IReadOnlyList<ManualNode> BuildTree()
    {
        var root = new Builder(Title, null, 0);
        var pages = new Dictionary<string, Builder>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, Builder>(StringComparer.OrdinalIgnoreCase);

        Builder ContainerFor(string folder)
        {
            if (folder.Length == 0)
            {
                return root;
            }

            if (pages.TryGetValue(folder + ".md", out var page))
            {
                return page;
            }

            if (!folders.TryGetValue(folder, out var node))
            {
                node = new Builder(TitleOf(folder), null, SortIndex(folder));
                folders[folder] = node;
                ContainerFor(DirectoryOf(folder)).Children.Add(node);
            }

            return node;
        }

        // Parents first, so a page "calculation.md" exists before its children in "calculation/".
        foreach (var path in _pages.Keys.OrderBy(k => k.Count(c => c == '/')).ThenBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var node = new Builder(TitleOf(path), path, SortIndex(path));
            pages[path] = node;
            ContainerFor(DirectoryOf(path)).Children.Add(node);
        }

        return root.Build().Children;
    }

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLink();
}
