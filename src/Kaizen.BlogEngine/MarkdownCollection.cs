using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Kaizen.BlogEngine;

/// <summary>A folder of markdown files with YAML front matter, served under one url prefix. See <see cref="IContentCollection"/>.</summary>
public sealed class MarkdownCollection : IContentCollection
{
    private readonly CollectionOptions _options;
    private readonly string _folder;
    private readonly Lazy<IReadOnlyList<ContentPage>> _pages;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public MarkdownCollection(CollectionOptions options, string contentRoot)
    {
        _options = options;
        _folder = Path.IsPathRooted(options.ContentPath) ? options.ContentPath : Path.Combine(contentRoot, options.ContentPath);
        _pages = new Lazy<IReadOnlyList<ContentPage>>(Load);
    }

    public string Name => _options.Name;
    public string UrlPrefix => _options.UrlPrefix;
    public CollectionOptions Options => _options;
    public IReadOnlyList<ContentPage> GetAll() => _pages.Value;
    public ContentPage? GetBySlug(string slug) => _pages.Value.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.Ordinal));
    public IReadOnlyList<ContentPage> GetByTag(string tag) =>
        _pages.Value.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
    public IReadOnlyList<string> GetTags() =>
        _pages.Value.SelectMany(p => p.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

    private IReadOnlyList<ContentPage> Load()
    {
        if (!Directory.Exists(_folder)) return Array.Empty<ContentPage>();
        var list = new List<ContentPage>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var post = Parse(file);
            if (post.Draft && !_options.IncludeDrafts) continue;
            if (list.Any(p => p.Slug == post.Slug)) throw new InvalidOperationException($"Duplicate post slug '{post.Slug}' ({file}).");
            list.Add(post);
        }
        return list.OrderByDescending(p => p.Date).ThenBy(p => p.Slug, StringComparer.Ordinal).ToList();
    }

    private ContentPage Parse(string file)
    {
        var doc = Markdown.Parse(File.ReadAllText(file), Pipeline);
        var block = doc.Descendants<YamlFrontMatterBlock>().FirstOrDefault();   // Markdig finds the --- ... --- block
        if (block is null) throw new InvalidOperationException($"{file}: missing or empty YAML front matter (--- ... ---); 'title' and 'date' are required.");
        var yaml = string.Join("\n", block.Lines.Lines.Take(block.Lines.Count).Select(l => l.ToString()));
        var fm = FrontMatterParser.Parse(yaml, file, fenceLine: block.Line + 1);

        var title = fm.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException($"{file}: front matter 'title' is required.");
        if (!TryDate(fm.Date, out var date)) throw new InvalidOperationException($"{file}: front matter 'date' is required (yyyy-MM-dd or ISO date-time).");
        var hasUpdated = TryDate(fm.Updated, out var updated);
        if (!string.IsNullOrWhiteSpace(fm.Updated) && !hasUpdated) throw new InvalidOperationException($"{file}: front matter 'updated' is not a date: '{fm.Updated}'.");
        var slug = (string.IsNullOrWhiteSpace(fm.Slug) ? Path.GetFileNameWithoutExtension(file) : fm.Slug).Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new InvalidOperationException($"{file}: slug '{slug}' must be lowercase-kebab.");

        return new ContentPage(slug, title, date, hasUpdated ? updated : null, NullIfBlank(fm.Description), NullIfBlank(fm.Author),
            fm.Tags ?? new List<string>(), fm.Draft, Markdown.ToHtml(doc, Pipeline), file)
        { Url = $"{_options.UrlPrefix.TrimEnd('/')}/{slug}/", Layout = NullIfBlank(fm.Layout), Collection = _options.Name };
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryDate(string? s, out DateTimeOffset d)
    {
        d = default;
        return s is not null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d);
    }
}
