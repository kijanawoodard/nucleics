using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Kaizen.BlogEngine;

public sealed class MarkdownPostService : IPostService
{
    private readonly MarkdownContentOptions _options;
    private readonly string _folder;
    private readonly Lazy<IReadOnlyList<Post>> _posts;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public MarkdownPostService(MarkdownContentOptions options, string contentRoot)
    {
        _options = options;
        _folder = Path.IsPathRooted(options.ContentPath) ? options.ContentPath : Path.Combine(contentRoot, options.ContentPath);
        _posts = new Lazy<IReadOnlyList<Post>>(Load);
    }

    public string UrlPrefix => _options.UrlPrefix;
    public IReadOnlyList<Post> GetAll() => _posts.Value;
    public Post? GetBySlug(string slug) => _posts.Value.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.Ordinal));
    public IReadOnlyList<Post> GetByTag(string tag) =>
        _posts.Value.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
    public IReadOnlyList<string> GetTags() =>
        _posts.Value.SelectMany(p => p.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

    private IReadOnlyList<Post> Load()
    {
        if (!Directory.Exists(_folder)) return Array.Empty<Post>();
        var list = new List<Post>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var post = Parse(file);
            if (post.Draft && !_options.IncludeDrafts) continue;
            if (list.Any(p => p.Slug == post.Slug)) throw new InvalidOperationException($"Duplicate post slug '{post.Slug}' ({file}).");
            list.Add(post);
        }
        return list.OrderByDescending(p => p.Date).ThenBy(p => p.Slug, StringComparer.Ordinal).ToList();
    }

    private Post Parse(string file)
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

        return new Post(slug, title, date, hasUpdated ? updated : null, NullIfBlank(fm.Description), NullIfBlank(fm.Author),
            fm.Tags ?? new List<string>(), fm.Draft, Markdown.ToHtml(doc, Pipeline), file)
        { Url = $"{_options.UrlPrefix.TrimEnd('/')}/{slug}/" };
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryDate(string? s, out DateTimeOffset d)
    {
        d = default;
        return s is not null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d);
    }
}
